#!/usr/bin/env python3
"""
changelog-update.py — Scan merged PRs for :cl: blocks and append new entries
to HonksquadChangelog.yml.

Usage:
    python3 scripts/changelog-update.py <changelog.yml> <prs.json> <repo>

Arguments:
    changelog.yml  Path to HonksquadChangelog.yml
    prs.json       JSON from: gh pr list --state merged --limit 500
                              --json number,mergedAt,author,body
    repo           GitHub repo slug, e.g. HellWatcher/honksquad-ss14
"""

import json
import re
import sys

TYPE_MAP = {"add": "Add", "tweak": "Tweak", "fix": "Fix", "remove": "Remove"}


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


def format_entry(entry, wrap=110):
    lines = [f"- author: {entry['author']}", "  changes:"]
    for ch in entry["changes"]:
        msg_yaml = yaml_message(ch["message"])
        prefix = f"  - message: {msg_yaml}"
        if len(prefix) > wrap:
            words = msg_yaml.split()
            line1, cur = [], "  - message: "
            for w in words:
                if len(cur + w) > wrap and line1:
                    break
                line1.append(w)
                cur += w + " "
            lines.append(f"  - message: {' '.join(line1)}")
            lines.append(f"      {' '.join(words[len(line1):])}")
        else:
            lines.append(prefix)
        lines.append(f"    type: {ch['type']}")
    lines.append(f"  time: '{entry['time']}'")
    lines.append(f"  url: {entry['url']}")
    return "\n".join(lines)


def gh_time(ts):
    return ts.rstrip("Z") + ".0000000+00:00"


def main():
    if len(sys.argv) != 4:
        print(f"Usage: {sys.argv[0]} <changelog.yml> <prs.json> <repo>")
        sys.exit(1)

    changelog_path, prs_path, repo = sys.argv[1], sys.argv[2], sys.argv[3]

    with open(changelog_path, encoding="utf-8") as f:
        existing = f.read()

    existing_urls = set(
        re.findall(
            r"url: (https://github\.com/[^/]+/[^/]+/pull/\d+)", existing
        )
    )

    with open(prs_path) as f:
        prs = sorted(json.load(f), key=lambda p: p["mergedAt"])

    new_entries = []
    for pr in prs:
        url = f"https://github.com/{repo}/pull/{pr['number']}"
        if url in existing_urls:
            continue
        changes = parse_cl_block(pr.get("body", ""))
        if not changes:
            continue
        new_entries.append(
            {
                "author": pr["author"]["login"],
                "changes": changes,
                "time": gh_time(pr["mergedAt"]),
                "url": url,
            }
        )

    if not new_entries:
        print(f"No new entries ({len(existing_urls)} PRs already tracked).")
        return

    with open(changelog_path, "a", encoding="utf-8") as f:
        for entry in new_entries:
            f.write(format_entry(entry) + "\n")

    urls = [e["url"] for e in new_entries]
    print(f"Added {len(new_entries)} new entries:")
    for u in urls:
        print(f"  {u}")


if __name__ == "__main__":
    main()
