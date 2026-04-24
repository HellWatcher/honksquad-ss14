#!/usr/bin/env python3
"""Audit upstream C# files on the fork for HONK discipline vs upstream.

Categorizes each file the same way as yaml_drift.py, but scoped to .cs files
outside fork-owned directories. The blocking signals in PR mode are:
  * new drift introduced by this PR outside HONK START/END blocks
  * malformed HONK blocks (unbalanced or nested)

Modes:
  (default)           Full scan of every upstream .cs file on --fork-ref.
  --pr-mode BASE_REF  Scan only .cs files changed between BASE_REF and HEAD.

Usage:
  python3 scripts/honk/cs_audit.py
  python3 scripts/honk/cs_audit.py --pr-mode origin/release
"""

from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass

from common import (
    DRIFT_CATEGORIES,
    HONK_START,
    balanced_honk,
    git_show,
    inline_honk_lines,
    is_fork_owned,
    pr_new_drift,
    pr_new_inline,
    sh,
    unmarked_hunks,
)

DEFAULT_FORK_REF = "origin/release"
DEFAULT_UPSTREAM_REF = "origin/upstream/stable"

CS_ROOTS = (
    "Content.Client/",
    "Content.Server/",
    "Content.Shared/",
    "Content.Server.Database/",
    "Content.IntegrationTests/",
    "Content.Tests/",
    "Content.Packaging/",
    "Content.YAMLLinter/",
    "Content.Benchmarks/",
    "Content.MapRenderer/",
    "Content.Replay/",
)


def is_in_scope(path: str) -> bool:
    if not path.endswith(".cs"):
        return False
    if not any(path.startswith(root) for root in CS_ROOTS):
        return False
    if is_fork_owned(path):
        return False
    return True


def list_all_cs(fork_ref: str) -> list[str]:
    out = sh("git", "ls-tree", "-r", "--name-only", fork_ref)
    return [line for line in out.splitlines() if is_in_scope(line)]


def list_changed_cs(base_ref: str, fork_ref: str) -> list[str]:
    out = sh(
        "git",
        "diff",
        "--name-only",
        "--diff-filter=d",
        f"{base_ref}...{fork_ref}",
    )
    return [line for line in out.splitlines() if is_in_scope(line)]


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

    ok, msg = balanced_honk(release)
    if not ok:
        return Result(path, "MALFORMED-HONK", msg)

    inline = inline_honk_lines(release)
    if inline:
        preview = ", ".join(f"line {n}" for n in inline[:3])
        if len(inline) > 3:
            preview += f" (+{len(inline) - 3} more)"
        return Result(path, "INLINE-HONK", preview)

    if release == upstream:
        return Result(path, "IDENTICAL")

    has_honk = bool(HONK_START.search(release))
    bad = unmarked_hunks(release, upstream)

    if not bad:
        if has_honk:
            return Result(path, "HONK-ONLY")
        return Result(path, "REFORMAT-ONLY")

    if has_honk:
        return Result(path, "MIXED", f"{len(bad)} unmarked hunk(s) outside HONK")
    return Result(path, "CONTENT-NO-HONK", f"{len(bad)} unmarked content change(s)")


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
            "INLINE-HONK",
            "MALFORMED-HONK",
            "MISSING-ON-FORK",
        ]
        for cat in order:
            items = buckets.get(cat, [])
            print(f"  {cat:<20s} {len(items):>5d}")
        print()

    reportable = ("MALFORMED-HONK", "INLINE-HONK") + DRIFT_CATEGORIES
    for cat in reportable:
        items = buckets.get(cat, [])
        if not items:
            continue
        print(f"=== {cat} ({len(items)}) ===")
        for r in items:
            suffix = f"  [{r.detail}]" if r.detail else ""
            print(f"  {r.path}{suffix}")
        print()


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    p.add_argument(
        "--pr-mode",
        metavar="BASE_REF",
        help="Scan only .cs files changed between BASE_REF and --fork-ref "
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
        files = list_changed_cs(args.pr_mode, fork_ref)
        header = (
            f"PR mode: scanning {len(files)} upstream C# file(s) "
            f"changed between {args.pr_mode} and {fork_ref}"
        )
    else:
        files = list_all_cs(fork_ref)
        header = f"Full scan: {len(files)} upstream C# file(s) on {fork_ref}"

    print(header)
    print(f"Compared against {args.upstream_ref}")
    print()

    results = [classify(f, fork_ref, args.upstream_ref) for f in files]

    print_summary(results, verbose=not pr_mode)

    if pr_mode:
        new_drift: list[tuple[Result, set[str]]] = []
        preexisting_drift: list[Result] = []
        malformed: list[Result] = []
        new_inline: list[tuple[Result, set[str]]] = []
        preexisting_inline: list[Result] = []
        for r in results:
            if r.category == "MALFORMED-HONK":
                malformed.append(r)
                continue
            if r.category == "INLINE-HONK":
                added_inline = pr_new_inline(r.path, args.pr_mode, fork_ref)
                if added_inline:
                    new_inline.append((r, added_inline))
                else:
                    preexisting_inline.append(r)
                continue
            if r.category not in DRIFT_CATEGORIES:
                continue
            added = pr_new_drift(r.path, args.pr_mode, fork_ref, args.upstream_ref)
            if added:
                new_drift.append((r, added))
            else:
                preexisting_drift.append(r)

        if preexisting_drift or preexisting_inline:
            total = len(preexisting_drift) + len(preexisting_inline)
            print(f"=== PRE-EXISTING DRIFT ({total}, " "not introduced by this PR) ===")
            for r in preexisting_drift:
                print(f"  {r.path}  [{r.category}]")
            for r in preexisting_inline:
                print(f"  {r.path}  [INLINE-HONK: {r.detail}]")
            print()

        failed = False
        if malformed:
            print(f"=== MALFORMED HONK BLOCKS ({len(malformed)}) ===")
            for r in malformed:
                print(f"  {r.path}  [{r.detail}]")
            print()
            failed = True

        if new_inline:
            print(
                f"=== NEW INLINE HONK COMMENTS INTRODUCED BY THIS PR "
                f"({len(new_inline)}) ==="
            )
            for result, added_inline in new_inline:
                print(f"  {result.path}  [{result.detail}]")
                for line in sorted(added_inline)[:3]:
                    print(f"      + {line[:160]}")
                if len(added_inline) > 3:
                    print(f"      ... ({len(added_inline) - 3} more)")
            print()
            failed = True

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
            failed = True

        if failed:
            print(
                "FAIL: this PR has HONK problems.\n"
                "Wrap fork changes in `//HONK START ... //HONK END` blocks — "
                "inline `//HONK` comments are not accepted. Every START must "
                "have a matching END. See CONTRIBUTING.md § "
                "'Marking your changes'."
            )
            return 1

        print("OK: no new upstream C# drift introduced by this PR.")
        return 0

    drift_count = sum(
        1
        for r in results
        if r.category in DRIFT_CATEGORIES
        or r.category in ("MALFORMED-HONK", "INLINE-HONK")
    )
    return 1 if drift_count else 0


if __name__ == "__main__":
    sys.exit(main())
