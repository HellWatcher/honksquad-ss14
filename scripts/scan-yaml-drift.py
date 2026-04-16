#!/usr/bin/env python3
"""
Scan upstream YAML files on the fork for drift vs upstream.

Categorizes each file as one of:
  IDENTICAL           — fork matches upstream exactly
  HONK-ONLY           — fork diverges only inside HONK START/END blocks (clean)
  REFORMAT-ONLY       — fork diverges but content matches upstream (pure drift)
  MIXED               — fork has HONK blocks AND drift outside them (needs fix)
  CONTENT-NO-HONK     — fork has non-HONK content changes (un-marked edits)
  FORK-NEW            — file does not exist in upstream (fork-only)

Modes:
  (default)           Full scan of every upstream YAML on --fork-ref.
  --pr-mode BASE_REF  Scan only YAML files changed between BASE_REF and HEAD
                      (--fork-ref defaults to HEAD in this mode). Suitable for
                      running on a PR to catch newly-introduced drift.

Exit code is non-zero when drift is found (REFORMAT-ONLY, MIXED,
or CONTENT-NO-HONK).

Usage:
  python3 scripts/scan-yaml-drift.py
  python3 scripts/scan-yaml-drift.py --pr-mode origin/release
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass

DEFAULT_FORK_REF = "origin/release"
DEFAULT_UPSTREAM_REF = "upstream/master"

# Paths we skip: fork-owned YAML and machine-generated map YAML.
FORK_OWNED = re.compile(r"(^|/)@RussStation/|^Resources/Maps/")

HONK_START = re.compile(r"HONK\s*START", re.IGNORECASE)
HONK_END = re.compile(r"HONK\s*END", re.IGNORECASE)
HONK_LINE = re.compile(r"//\s*HONK\b|#\s*HONK\b", re.IGNORECASE)

DRIFT_CATEGORIES = ("REFORMAT-ONLY", "MIXED", "CONTENT-NO-HONK")


def sh(*args: str) -> str:
    return subprocess.check_output(args, text=True, stderr=subprocess.DEVNULL)


def git_show(ref: str, path: str) -> str | None:
    try:
        return subprocess.check_output(
            ["git", "show", f"{ref}:{path}"],
            text=True,
            stderr=subprocess.DEVNULL,
        )
    except subprocess.CalledProcessError:
        return None


def is_in_scope(path: str) -> bool:
    if not (path.endswith(".yml") or path.endswith(".yaml")):
        return False
    if not path.startswith("Resources/Prototypes/"):
        return False
    if FORK_OWNED.search(path):
        return False
    return True


def list_all_yaml(fork_ref: str) -> list[str]:
    out = sh("git", "ls-tree", "-r", "--name-only", fork_ref, "Resources/Prototypes")
    return [line for line in out.splitlines() if is_in_scope(line)]


def list_changed_yaml(base_ref: str, fork_ref: str) -> list[str]:
    # diff-filter=d excludes deletions — deleted files can't drift.
    out = sh(
        "git",
        "diff",
        "--name-only",
        "--diff-filter=d",
        f"{base_ref}...{fork_ref}",
    )
    return [line for line in out.splitlines() if is_in_scope(line)]


def strip_honk_blocks(text: str) -> str:
    """Remove HONK START..HONK END blocks and single-line HONK comments."""
    out_lines = []
    in_block = False
    for line in text.splitlines():
        if HONK_START.search(line):
            in_block = True
            continue
        if HONK_END.search(line):
            in_block = False
            continue
        if in_block:
            continue
        if HONK_LINE.search(line):
            # single-line HONK marker sitting alone on a line — drop it
            continue
        out_lines.append(line)
    return "\n".join(out_lines)


def whitespace_normalize(text: str) -> str:
    """Collapse all whitespace within each line, drop blank lines. Used to
    compare content while ignoring reformatting."""
    out = []
    for line in text.splitlines():
        collapsed = re.sub(r"\s+", " ", line).strip()
        if collapsed:
            out.append(collapsed)
    return "\n".join(out)


@dataclass
class Result:
    path: str
    category: str
    detail: str = ""


def classify(path: str, fork_ref: str, upstream_ref: str) -> Result:
    upstream = git_show(upstream_ref, path)
    release = git_show(fork_ref, path)

    if release is None:
        return Result(path, "MISSING-ON-FORK")
    if upstream is None:
        return Result(path, "FORK-NEW")

    if release == upstream:
        return Result(path, "IDENTICAL")

    has_honk = bool(HONK_START.search(release) or HONK_LINE.search(release))

    stripped_release = strip_honk_blocks(release)
    norm_stripped = whitespace_normalize(stripped_release)
    norm_upstream = whitespace_normalize(upstream)

    if norm_stripped == norm_upstream:
        if has_honk:
            return Result(path, "HONK-ONLY")
        return Result(path, "REFORMAT-ONLY")

    if has_honk:
        return Result(path, "MIXED", "drift outside HONK")
    return Result(path, "CONTENT-NO-HONK", "unmarked content changes")


def print_summary(results: list[Result], *, verbose: bool) -> None:
    buckets: dict[str, list[Result]] = {}
    for r in results:
        buckets.setdefault(r.category, []).append(r)

    if verbose:
        order = [
            "IDENTICAL",
            "HONK-ONLY",
            "FORK-NEW",
            "REFORMAT-ONLY",
            "MIXED",
            "CONTENT-NO-HONK",
            "MISSING-ON-FORK",
        ]
        for cat in order:
            items = buckets.get(cat, [])
            print(f"  {cat:<20s} {len(items):>5d}")
        print()

    for cat in DRIFT_CATEGORIES:
        items = buckets.get(cat, [])
        if not items:
            continue
        print(f"=== {cat} ({len(items)}) ===")
        for r in items:
            suffix = f"  [{r.detail}]" if r.detail else ""
            print(f"  {r.path}{suffix}")
        print()


def drift_lines(fork_text: str, upstream_text: str) -> set[str]:
    """Return the set of non-HONK lines present in the fork but not upstream.

    Whitespace-normalized. Blank lines are dropped. This is the file's
    upstream-relative drift footprint: lines the fork added or changed outside
    HONK-wrapped regions."""
    fork_norm = whitespace_normalize(strip_honk_blocks(fork_text))
    upstream_norm = whitespace_normalize(upstream_text)
    fork_set = {ln for ln in fork_norm.split("\n") if ln}
    upstream_set = {ln for ln in upstream_norm.split("\n") if ln}
    return fork_set - upstream_set


def new_drift_on_path(
    path: str, base_ref: str, fork_ref: str, upstream_ref: str
) -> set[str]:
    """Return lines of drift that fork_ref introduces vs base_ref.

    Computes the drift footprint at each side against upstream, then subtracts.
    A non-empty return means the PR added unmarked content or reformatted
    upstream lines that weren't drifting before."""
    head = git_show(fork_ref, path) or ""
    base = git_show(base_ref, path) or ""
    upstream = git_show(upstream_ref, path)
    if upstream is None:
        return set()
    return drift_lines(head, upstream) - drift_lines(base, upstream)


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    p.add_argument(
        "--pr-mode",
        metavar="BASE_REF",
        help="Scan only YAML files changed between BASE_REF and --fork-ref "
        "(fork-ref defaults to HEAD in this mode).",
    )
    p.add_argument(
        "--fork-ref",
        help=f"Fork-side ref to scan. Default: {DEFAULT_FORK_REF} "
        f"(or HEAD when --pr-mode is set).",
    )
    p.add_argument(
        "--upstream-ref",
        default=DEFAULT_UPSTREAM_REF,
        help=f"Upstream ref to compare against. Default: {DEFAULT_UPSTREAM_REF}.",
    )
    return p.parse_args()


def main() -> int:
    args = parse_args()

    pr_mode = args.pr_mode is not None
    fork_ref = args.fork_ref or ("HEAD" if pr_mode else DEFAULT_FORK_REF)

    if pr_mode:
        files = list_changed_yaml(args.pr_mode, fork_ref)
        header = (
            f"PR mode: scanning {len(files)} upstream YAML file(s) "
            f"changed between {args.pr_mode} and {fork_ref}"
        )
    else:
        files = list_all_yaml(fork_ref)
        header = f"Full scan: {len(files)} upstream YAML file(s) on {fork_ref}"

    print(header)
    print(f"Compared against {args.upstream_ref}")
    print()

    results = [classify(f, fork_ref, args.upstream_ref) for f in files]

    print_summary(results, verbose=not pr_mode)

    if pr_mode:
        new_drift: list[tuple[Result, set[str]]] = []
        preexisting_drift: list[Result] = []
        for r in results:
            if r.category not in DRIFT_CATEGORIES:
                continue
            added = new_drift_on_path(r.path, args.pr_mode, fork_ref, args.upstream_ref)
            if added:
                new_drift.append((r, added))
            else:
                preexisting_drift.append(r)

        if preexisting_drift:
            print(
                f"=== PRE-EXISTING DRIFT ({len(preexisting_drift)}, "
                "not introduced by this PR) ==="
            )
            for r in preexisting_drift:
                print(f"  {r.path}  [{r.category}]")
            print()

        if new_drift:
            print(f"=== NEW DRIFT INTRODUCED BY THIS PR ({len(new_drift)}) ===")
            for entry in new_drift:
                result, added = entry
                print(f"  {result.path}  [{result.category}]")
                for line in sorted(added)[:5]:
                    print(f"      + {line[:160]}")
                if len(added) > 5:
                    print(f"      ... ({len(added) - 5} more)")
            print()
            print(
                "FAIL: this PR introduces upstream YAML drift.\n"
                "Wrap fork changes in `# HONK START ... # HONK END` and do not "
                "reformat surrounding upstream lines. See CLAUDE.md § "
                "'YAML Formatting in Upstream Files'."
            )
            return 1

        print("OK: no new upstream YAML drift introduced by this PR.")
        return 0

    drift_count = sum(1 for r in results if r.category in DRIFT_CATEGORIES)
    return 1 if drift_count else 0


if __name__ == "__main__":
    sys.exit(main())
