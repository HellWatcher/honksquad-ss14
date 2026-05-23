#!/usr/bin/env python3
"""
changelog-update.py — Scan merged PRs for :cl: blocks and append new entries
to HonksquadChangelog.yml.

Usage:
    python3 scripts/changelog-update.py <changelog.yml> <prs.json> <repo>
        [--watermark-file <path>] [--deny-file <path>]

Arguments:
    changelog.yml       Path to HonksquadChangelog.yml
    prs.json            JSON from: gh pr list --state merged --limit 500
                                  --json number,mergedAt,author,body
    repo                GitHub repo slug, e.g. HellWatcher/honksquad-ss14

Options:
    --watermark-file    Path to .changelog-watermark holding an ISO 8601
                        UTC timestamp (e.g. 2026-05-02T22:57:17Z). Only PRs
                        with mergedAt > watermark are processed. Updated to
                        the highest mergedAt seen after each run.

                        Legacy numeric watermarks (a bare PR number) are
                        detected and ignored — the script falls back to the
                        URL-dedupe set in changelog.yml for that run, then
                        rewrites the file in timestamp form.
    --deny-file         Path to .changelog-deny (one PR number per line).
                        Matching PRs are skipped even if they have :cl: blocks.

Why mergedAt and not PR number: PRs don't merge in number order. A merge
order watermark advances monotonically (GitHub stamps mergedAt at merge);
a PR-number watermark can leap past PRs that are still open and
permanently skip them once they later merge.
"""

import json
import re
import sys

TYPE_MAP = {"add": "Add", "tweak": "Tweak", "fix": "Fix", "remove": "Remove"}
ISO_TS_RE = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")


def parse_cl_block(body):
    if not body:
        return []
    m = re.search(r":cl:.*?\n(.*?)(?:/:cl:|\Z)", body, re.DOTALL | re.IGNORECASE)
    if not m:
        return []
    entries = []
    for line in m.group(1).splitlines():
        hit = re.match(r"^\s*-\s+(add|tweak|fix|remove):\s+(.+)", line, re.IGNORECASE)
        if hit:
            typ = TYPE_MAP[hit.group(1).lower()]
            msg = hit.group(2).strip()
            if not msg.endswith("."):
                msg += "."
            msg = msg[0].upper() + msg[1:]
            entries.append({"message": msg, "type": typ})
    return entries


def yaml_message(text):
    if "'" in text:
        return "'" + text.replace("'", "''") + "'"
    if re.match(r'^[{}\[\]#&*!|>\'"%@`?:\-]', text):
        return "'" + text + "'"
    if re.search(r": |:$", text):
        return "'" + text + "'"
    if " #" in text:
        return "'" + text + "'"
    return text


def format_entry(entry):
    lines = [f"- author: {entry['author']}", "  changes:"]
    for ch in entry["changes"]:
        msg_yaml = yaml_message(ch["message"])
        lines.append(f"  - message: {msg_yaml}")
        lines.append(f"    type: {ch['type']}")
    lines.append(f"  time: '{entry['time']}'")
    lines.append(f"  url: {entry['url']}")
    return "\n".join(lines)


def gh_time(ts):
    return ts.rstrip("Z") + ".0000000+00:00"


def read_watermark(path):
    """Return ISO timestamp string, or '' if missing/legacy/invalid.

    Legacy numeric watermarks (a bare PR number from the pre-fix script) are
    treated as empty so the URL-dedupe in changelog.yml carries the run, and
    the file gets rewritten in timestamp form on the next successful update.
    """
    try:
        with open(path) as f:
            raw = f.read().strip()
    except FileNotFoundError:
        return ""
    if ISO_TS_RE.match(raw):
        return raw
    if raw.isdigit():
        print(f"  Note: legacy numeric watermark ({raw}) ignored; using URL dedupe.")
        return ""
    return ""


def write_watermark(path, value):
    with open(path, "w") as f:
        f.write(f"{value}\n")


def read_deny(path):
    denied = set()
    try:
        with open(path) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#"):
                    denied.add(int(line))
    except FileNotFoundError:
        pass
    return denied


def main():
    args = sys.argv[1:]
    if len(args) < 3:
        print(f"Usage: {sys.argv[0]} <changelog.yml> <prs.json> <repo> [--watermark-file <path>] [--deny-file <path>]")
        sys.exit(1)

    changelog_path, prs_path, repo = args[0], args[1], args[2]
    watermark_file = None
    deny_file = None

    i = 3
    while i < len(args):
        if args[i] == "--watermark-file" and i + 1 < len(args):
            watermark_file = args[i + 1]
            i += 2
        elif args[i] == "--deny-file" and i + 1 < len(args):
            deny_file = args[i + 1]
            i += 2
        else:
            i += 1

    watermark = read_watermark(watermark_file) if watermark_file else ""
    denied = read_deny(deny_file) if deny_file else set()

    with open(changelog_path, encoding="utf-8") as f:
        existing = f.read()

    existing_urls = set(
        re.findall(r"url: (https://github\.com/[^/]+/[^/]+/pull/\d+)", existing)
    )

    with open(prs_path) as f:
        prs = sorted(json.load(f), key=lambda p: p["mergedAt"])

    skipped_watermark = 0
    skipped_deny = 0
    skipped_existing = 0
    new_entries = []
    max_merged = watermark

    for pr in prs:
        num = pr["number"]
        merged_at = pr["mergedAt"]
        if merged_at > max_merged:
            max_merged = merged_at

        if watermark and merged_at <= watermark:
            skipped_watermark += 1
            continue

        if num in denied:
            skipped_deny += 1
            continue

        url = f"https://github.com/{repo}/pull/{num}"
        if url in existing_urls:
            skipped_existing += 1
            continue

        changes = parse_cl_block(pr.get("body", ""))
        if not changes:
            continue

        new_entries.append({
            "author": pr["author"]["login"],
            "changes": changes,
            "time": gh_time(merged_at),
            "url": url,
        })

    print(f"Scanned: {len(prs)} PRs total")
    print(f"  Skipped by watermark (mergedAt <= {watermark or 'n/a'}): {skipped_watermark}")
    print(f"  Skipped by denylist: {skipped_deny}")
    print(f"  Skipped (already in changelog): {skipped_existing}")
    print(f"  New entries: {len(new_entries)}")

    if not new_entries:
        if watermark_file and max_merged:
            write_watermark(watermark_file, max_merged)
        return

    with open(changelog_path, "a", encoding="utf-8") as f:
        for entry in new_entries:
            f.write(format_entry(entry) + "\n")

    if watermark_file and max_merged:
        write_watermark(watermark_file, max_merged)
        print(f"Watermark updated: {watermark or '(empty)'} -> {max_merged}")

    print("Added:")
    for e in new_entries:
        print(f"  {e['url']}")


if __name__ == "__main__":
    main()
