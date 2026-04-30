#!/usr/bin/env python3
"""
merge-changelog.py — Combine upstream Changelog.yml with HonksquadChangelog.yml.

Reads both files, sorts all entries by time, assigns sequential IDs continuing
from the last upstream entry, and writes the result to Changelog.yml.

Run before packaging. After packaging, restore Changelog.yml with:
    git checkout Resources/Changelog/Changelog.yml

Usage:
    python3 scripts/merge-changelog.py
    python3 scripts/merge-changelog.py --dry-run   (print stats, don't write)
"""

import re
import sys

UPSTREAM = "Resources/Changelog/Changelog.yml"
FORK = "Resources/Changelog/HonksquadChangelog.yml"
DRY_RUN = "--dry-run" in sys.argv


def parse_entries(text):
    """Split changelog text into a list of raw entry strings."""
    raw = re.split(r"(?=^- author:)", text, flags=re.MULTILINE)
    return [e for e in raw if e.strip() and e.strip().startswith("- author:")]


def entry_time(raw):
    m = re.search(r"^  time: '([^']+)'", raw, re.MULTILINE)
    return m.group(1) if m else ""


def entry_id(raw):
    m = re.search(r"^  id: (\d+)$", raw, re.MULTILINE)
    return int(m.group(1)) if m else None


def set_id(raw, new_id):
    """Replace or insert the id: field before the time: line."""
    if re.search(r"^  id: \d+$", raw, re.MULTILINE):
        return re.sub(r"^  id: \d+$", f"  id: {new_id}", raw, flags=re.MULTILINE)
    # No id field: insert before time:
    return re.sub(r"(^  time:)", f"  id: {new_id}\n\\1", raw, flags=re.MULTILINE)


def main():
    with open(UPSTREAM, "r", encoding="utf-8-sig") as f:
        upstream_text = f.read()

    with open(FORK, "r", encoding="utf-8") as f:
        fork_text = f.read()
        # Strip leading "Entries:\n" header if present
        fork_text = re.sub(r"^Entries:\s*\n", "", fork_text)

    upstream_entries = parse_entries(upstream_text)
    fork_entries = parse_entries(fork_text)

    # Last upstream ID is the base
    upstream_ids = [entry_id(e) for e in upstream_entries if entry_id(e) is not None]
    next_id = max(upstream_ids) + 1 if upstream_ids else 1

    # Sort fork entries by time, then assign sequential IDs
    fork_entries.sort(key=entry_time)
    numbered_fork = []
    for entry in fork_entries:
        numbered_fork.append(set_id(entry, next_id))
        next_id += 1

    if DRY_RUN:
        print(f"Upstream entries : {len(upstream_entries)}")
        print(f"Fork entries     : {len(fork_entries)}")
        print(f"Combined total   : {len(upstream_entries) + len(fork_entries)}")
        print(f"ID range (fork)  : {max(upstream_ids) + 1} - {next_id - 1}")
        return

    # Build output: BOM + header + upstream + fork
    output = "Entries:\n"
    for entry in upstream_entries:
        output += entry
    for entry in numbered_fork:
        output += entry

    with open(UPSTREAM, "w", encoding="utf-8-sig") as f:
        f.write(output)

    print(
        f"Merged {len(upstream_entries)} upstream + {len(fork_entries)} fork entries "
        f"(IDs {max(upstream_ids) + 1}-{next_id - 1}) into {UPSTREAM}."
    )


if __name__ == "__main__":
    main()
