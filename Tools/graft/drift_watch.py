#!/usr/bin/env python3
# SPDX-FileCopyrightText: honksquad-ss14 contributors
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Report donor drift for content grafted from other SS14 forks.

Reads `graft-manifest.yml`, fetches each donor named by a graft, and reports:

  * donor commits touching the graft's source paths since the recorded sha —
    upstream fixes we have not pulled, or divergence we have chosen to keep;
  * license drift — an SPDX header in our copy that no longer matches what the
    manifest recorded for that graft.

Exit status is 1 when anything is reported, so a scheduled workflow can branch
on it. A donor that cannot be fetched is an error, not silence: exit 2.

  python3 Tools/graft/drift_watch.py
  python3 Tools/graft/drift_watch.py --graft some-feature --no-fetch
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

import yaml

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
MANIFEST = REPO / "graft-manifest.yml"

# REUSE-IgnoreStart — the pattern below is what this script *searches for*, not a
# license declaration for the script itself; reuse lint would otherwise try to
# parse the regex as an SPDX expression and fail.
SPDX = re.compile(r"SPDX-License-Identifier:\s*(?P<id>[^\s*/#-]+(?:[-.\w+]*)*)")
# REUSE-IgnoreEnd

# Reading every blob under a grafted tree is pointless; headers live at the top.
HEADER_BYTES = 4096


def run(args: list[str], cwd: Path = REPO) -> subprocess.CompletedProcess:
    return subprocess.run(args, cwd=cwd, capture_output=True, text=True)


def fetch(donor: dict) -> str:
    """Fetch a donor ref into FETCH_HEAD, blobless so it stays cheap."""
    result = run(
        ["git", "fetch", "--no-tags", "--filter=blob:none", donor["url"], donor["ref"]]
    )
    if result.returncode != 0:
        raise RuntimeError(f"fetch failed for {donor['url']}: {result.stderr.strip()}")
    head = run(["git", "rev-parse", "FETCH_HEAD"])
    return head.stdout.strip()


def new_commits(since: str, head: str, paths: list[str]) -> list[str]:
    result = run(
        ["git", "log", "--no-merges", "--format=%h %ad %an: %s", "--date=short",
         f"{since}..{head}", "--", *paths]
    )
    if result.returncode != 0:
        # The recorded sha is not an ancestor we can reach — a rewritten donor
        # history, or a sha recorded from a fork we no longer fetch.
        raise RuntimeError(f"cannot diff {since[:12]}..{head[:12]}: {result.stderr.strip()}")
    return [line for line in result.stdout.splitlines() if line.strip()]


def licenses_in(paths: list[str]) -> set[str]:
    found: set[str] = set()
    for entry in paths:
        target = REPO / entry
        files = [target] if target.is_file() else sorted(target.rglob("*")) if target.exists() else []
        for path in files:
            if not path.is_file():
                continue
            try:
                head = path.read_bytes()[:HEADER_BYTES].decode("utf-8", "replace")
            except OSError:
                continue
            found.update(match.group("id").rstrip(",;") for match in SPDX.finditer(head))
    return found


def check(graft: dict, donors: dict, do_fetch: bool) -> tuple[list[str], bool]:
    lines: list[str] = []
    dirty = False
    donor = donors.get(graft["donor"])
    if donor is None:
        raise RuntimeError(f"graft {graft['id']!r} names unknown donor {graft['donor']!r}")

    lines.append(f"## {graft['id']}  ({donor['name']} @ {graft['synced_at'][:12]})")

    if do_fetch:
        head = fetch(donor)
        commits = new_commits(graft["synced_at"], head, graft["source_paths"])
        if commits:
            dirty = True
            lines.append(f"  {len(commits)} donor commit(s) touching grafted paths since sync:")
            lines += [f"    {c}" for c in commits[:25]]
            if len(commits) > 25:
                lines.append(f"    ... and {len(commits) - 25} more")
        else:
            lines.append("  donor unchanged on these paths")

    recorded = graft.get("license", "")
    actual = licenses_in(graft.get("local_paths", []))
    unexpected = {lic for lic in actual if lic != recorded}
    if unexpected:
        dirty = True
        lines.append(
            f"  license drift: manifest says {recorded}, files also carry "
            + ", ".join(sorted(unexpected))
        )
    elif not actual:
        lines.append(f"  no SPDX headers found locally; covered by REUSE.toml globs as {recorded}")

    return lines, dirty


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=MANIFEST)
    parser.add_argument("--graft", help="check only this graft id")
    parser.add_argument("--no-fetch", action="store_true", help="skip donor fetch, licenses only")
    opts = parser.parse_args()

    with opts.manifest.open(encoding="utf-8") as handle:
        manifest = yaml.safe_load(handle)

    donors = manifest.get("donors") or {}
    grafts = manifest.get("grafts") or []
    if opts.graft:
        grafts = [g for g in grafts if g["id"] == opts.graft]
        if not grafts:
            print(f"no graft with id {opts.graft!r}", file=sys.stderr)
            return 2

    if not grafts:
        print("No grafts recorded yet — nothing to watch.")
        return 0

    report: list[str] = []
    dirty = False
    for graft in grafts:
        try:
            lines, graft_dirty = check(graft, donors, not opts.no_fetch)
        except RuntimeError as error:
            print(f"error: {error}", file=sys.stderr)
            return 2
        report += lines + [""]
        dirty = dirty or graft_dirty

    print(f"Graft drift: {len(grafts)} graft(s) checked\n")
    print("\n".join(report).rstrip())
    return 1 if dirty else 0


if __name__ == "__main__":
    raise SystemExit(main())
