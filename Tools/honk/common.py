"""Shared helpers for HONK marker and upstream drift audits.

Two modes of analysis are supported by the audit scripts:
  * Full scan — walk every in-scope upstream file on the fork and classify.
  * PR mode  — scope to files changed in BASE..HEAD and separate drift the PR
               *introduces* from drift that already existed on BASE.

Both modes share the same whitespace-normalized line-set comparison used by
:func:`drift_lines` and :func:`pr_new_drift`.
"""

from __future__ import annotations

import re
import subprocess

# Directories that are fork-owned across the repo. These are always skipped —
# they don't need HONK markers because they have no upstream counterpart.
#   * `@RussStation/`  — YAML, textures, audio (the `@` sorts to the top)
#   * `RussStation/`   — C# (plain form, because `@` is invalid in namespaces)
#   * `Resources/Maps/` — machine-generated map YAML
FORK_OWNED = re.compile(
    r"(^|/)@RussStation/" r"|(^|/)RussStation/" r"|^Resources/Maps/"
)

HONK_START = re.compile(r"HONK\s*START", re.IGNORECASE)
HONK_END = re.compile(r"HONK\s*END", re.IGNORECASE)
HONK_LINE = re.compile(r"//\s*HONK\b|#\s*HONK\b")

DRIFT_CATEGORIES = ("REFORMAT-ONLY", "MIXED", "CONTENT-NO-HONK")


def sh(*args: str) -> str:
    return subprocess.check_output(args, text=True, stderr=subprocess.DEVNULL)


def git_show(ref: str, path: str) -> str | None:
    """Return file contents at ref, or None if the path doesn't exist there."""
    try:
        return subprocess.check_output(
            ["git", "show", f"{ref}:{path}"],
            text=True,
            stderr=subprocess.DEVNULL,
        )
    except subprocess.CalledProcessError:
        return None


def is_fork_owned(path: str) -> bool:
    return bool(FORK_OWNED.search(path))


def strip_honk_blocks(text: str) -> str:
    """Drop HONK START..HONK END blocks. Inline HONK comments are *not*
    stripped — they're disallowed and surface as drift."""
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
        out_lines.append(line)
    return "\n".join(out_lines)


def inline_honk_lines(text: str) -> list[int]:
    """Line numbers (1-indexed) of inline HONK comments — HONK markers that
    aren't a START or END of a block. Block-style `HONK START ... HONK END`
    is the only accepted form; bare `// HONK` or `# HONK` is a violation."""
    violations = []
    for i, line in enumerate(text.splitlines(), 1):
        if not HONK_LINE.search(line):
            continue
        if HONK_START.search(line) or HONK_END.search(line):
            continue
        violations.append(i)
    return violations


def whitespace_normalize(text: str) -> str:
    """Collapse intra-line whitespace and drop blank lines."""
    out = []
    for line in text.splitlines():
        collapsed = re.sub(r"\s+", " ", line).strip()
        if collapsed:
            out.append(collapsed)
    return "\n".join(out)


def balanced_honk(text: str) -> tuple[bool, str]:
    """Check HONK START/END are balanced and non-nested.

    Returns (ok, message). The message is empty when ok=True."""
    depth = 0
    open_line = 0
    for i, line in enumerate(text.splitlines(), 1):
        if HONK_START.search(line):
            if depth > 0:
                return False, (
                    f"line {i}: nested HONK START "
                    f"(already inside block opened at line {open_line})"
                )
            depth += 1
            open_line = i
        elif HONK_END.search(line):
            if depth == 0:
                return False, f"line {i}: HONK END without matching START"
            depth -= 1
    if depth > 0:
        return False, f"HONK START at line {open_line} never closed"
    return True, ""


def drift_lines(fork_text: str, reference_text: str) -> set[str]:
    """Non-HONK lines present in fork but not in reference.

    Whitespace-normalized; blank lines dropped. This is the fork file's
    upstream-relative drift footprint — the lines it added or changed outside
    HONK-wrapped regions compared to the reference."""
    fork_norm = whitespace_normalize(strip_honk_blocks(fork_text))
    reference_norm = whitespace_normalize(reference_text)
    fork_set = {ln for ln in fork_norm.split("\n") if ln}
    reference_set = {ln for ln in reference_norm.split("\n") if ln}
    return fork_set - reference_set


def pr_new_inline(path: str, base_ref: str, fork_ref: str) -> set[str]:
    """Inline HONK lines present in fork_ref but not in base_ref.

    Compared by whitespace-normalized line text. Used in PR mode to separate
    inline HONK the PR itself introduces from lines that were already on base."""
    head = git_show(fork_ref, path) or ""
    base = git_show(base_ref, path) or ""

    def _lines(text: str) -> set[str]:
        lines = text.splitlines()
        inline = inline_honk_lines(text)
        return {re.sub(r"\s+", " ", lines[n - 1]).strip() for n in inline}

    return _lines(head) - _lines(base)


def pr_new_drift(
    path: str, base_ref: str, fork_ref: str, reference_ref: str
) -> set[str]:
    """Drift introduced by fork_ref over base_ref, relative to reference_ref.

    Computes the drift footprint at each side against the reference and
    subtracts. An empty return means the PR added no unmarked content
    (though the file may still drift overall — that drift is pre-existing)."""
    head = git_show(fork_ref, path) or ""
    base = git_show(base_ref, path) or ""
    reference = git_show(reference_ref, path)
    if reference is None:
        return set()
    return drift_lines(head, reference) - drift_lines(base, reference)


def list_changed(base_ref: str, fork_ref: str, *suffixes: str) -> list[str]:
    """Files changed between base_ref and fork_ref (excluding deletions)
    whose path ends in one of the given suffixes."""
    out = sh(
        "git",
        "diff",
        "--name-only",
        "--diff-filter=d",
        f"{base_ref}...{fork_ref}",
    )
    return [
        line for line in out.splitlines() if any(line.endswith(s) for s in suffixes)
    ]
