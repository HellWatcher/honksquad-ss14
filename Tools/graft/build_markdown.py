#!/usr/bin/env python3
# SPDX-FileCopyrightText: honksquad-ss14 contributors
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Render fork-features.yml into FORK-CATALOG.md.

The Markdown is generated; edit the YAML instead. `--check` re-renders and
compares, so CI fails a PR that edits the Markdown by hand or forgets to
regenerate it after a YAML change.

  python3 Tools/graft/build_markdown.py
  python3 Tools/graft/build_markdown.py --check
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import catalog as cat

DEFAULT_OUT = cat.HERE / "FORK-CATALOG.md"

OWN = "●"
VENDORED = "○"
UPSTREAM_LABEL = {"core": "ships", "partial": "partial", "absent": "no"}

# Split so that `reuse lint` does not read the header this script *emits* as a
# license declaration for the script itself, which would fail with an invalid
# SPDX expression (the trailing comma of the list literal ends up in the value).
SPDX_TAG = "SPDX-License" "-Identifier"


def fork_table(forks: list[dict]) -> list[str]:
    lines = [
        "| Fork | License | Namespaces | Pass | Last push |",
        "| --- | --- | --- | --- | --- |",
    ]
    for fork in forks:
        name = f"[{fork['name']}]({fork['repo']})" if fork.get("repo") else fork["name"]
        namespaces = ", ".join(f"`{ns}`" for ns in fork.get("namespaces", [])) or "inline"
        label = cat.PASS_LABEL.get(fork.get("pass", "round-1"), "?")
        lines.append(
            f"| {name} | {fork.get('license', '?')} | {namespaces} | {label} "
            f"| {(fork.get('last_push') or '')[:10]} |"
        )
    return lines


def matrix(features: list[dict], columns: list[dict], category: str) -> list[str]:
    header = " | ".join(cat.tag(f) for f in columns)
    lines = [
        f"| Feature | Wizden | {header} |",
        "| --- | --- | " + " | ".join("---" for _ in columns) + " |",
    ]
    for feature in features:
        if feature["category"] != category:
            continue
        # The matrix is the one place carriers still get a cell: hiding them
        # would make a widely-adopted feature look like one fork's private toy.
        glyphs = {row["fork"]: OWN for row in cat.authors(feature)}
        glyphs.update({row["fork"]: VENDORED for row in cat.carriers(feature)})
        cells = [glyphs.get(fork["id"], "") for fork in columns]
        upstream = UPSTREAM_LABEL.get(feature.get("upstream", "absent"), "?")
        lines.append(
            f"| [{feature['name']}](#{feature['id']}) | {upstream} | " + " | ".join(cells) + " |"
        )
    return lines


SHAPE_LABEL = {
    "drop-in": "drop-in",
    "needs-upstream-edits": "needs upstream edits",
    "entangled": "entangled",
}


def assessment(feature: dict, entry: dict, with_name: bool) -> list[str]:
    """One gameplay assessment, with its verification alongside the claim.

    The verdict is never folded into the claim: a reader has to be able to see
    that a second pass disagreed, and on what.
    """
    covers = ", ".join(entry.get("covers") or []) or "?"
    head = f"[{feature['name']}](#{feature['id']}) — {covers}" if with_name else covers
    lines = [
        f"**{head}**",
        "",
        f"*{entry['round_impact']} · {SHAPE_LABEL.get(entry['graft_shape'], entry['graft_shape'])} · "
        f"{entry.get('confidence', '?')} confidence · {entry.get('adoption', 0)} forks carry it*",
        "",
        entry.get("gameplay", ""),
        "",
        f"- **Take** — {entry.get('take_because', '')}",
        f"- **Skip** — {entry.get('skip_because', '')}",
    ]
    if entry.get("touches"):
        lines.append(f"- **Touches** — {', '.join(entry['touches'])}")
    if entry.get("depends_on"):
        needs = ", ".join(f"[{dep}](#{dep})" for dep in entry["depends_on"])
        lines.append(f"- **Needs** — {needs}")

    check = cat.verdict(entry)
    if check is None:
        lines += ["- **Verification** — none; read this as a claim, not a finding."]
    else:
        note = f"- **Verified** — {check['gameplay_supported'].replace('-', ' ')}"
        if entry.get("graft_shape_claimed"):
            claimed = SHAPE_LABEL.get(entry["graft_shape_claimed"], entry["graft_shape_claimed"])
            note += f"; assessed as {claimed}, corrected by the verifier"
        lines.append(f"{note}. {check.get('why', '')}")
        for over in check.get("overclaims") or []:
            lines.append(f"  - unsupported: {over}")
    return lines + [""]


def detail(feature: dict) -> list[str]:
    lines = [f"### {feature['name']}", "", f'<a id="{feature["id"]}"></a>', ""]
    if feature.get("summary"):
        lines += [feature["summary"], ""]

    for entry in cat.assessments(feature):
        lines += assessment(feature, entry, with_name=False)

    authors = cat.authors(feature)
    carriers = cat.carriers(feature)
    if not authors and not carriers:
        return lines + ["No fork implementation catalogued.", ""]

    if authors:
        lines += ["| Fork | Calls it | Files | Primary path |", "| --- | --- | --- | --- |"]
        for row in authors:
            paths = row.get("paths", [])
            primary = f"`{paths[0]}`" if paths else ""
            extra = f" (+{len(paths) - 1})" if len(paths) > 1 else ""
            lines.append(
                f"| {row['fork']} | {row.get('local_name', '')} | {row.get('files', 0)} "
                f"| {primary}{extra} |"
            )
        lines.append("")
    else:
        lines += ["No fork authored this; every copy below is vendored from elsewhere.", ""]

    if carriers:
        also = ", ".join(f"{row['fork']} (via `{row['via']}`)" for row in carriers)
        lines += [f"Also carried by {len(carriers)}: {also}", ""]
    return lines


TIERS = (
    (
        "take-now",
        "Take now",
        "Verified as genuinely self-contained: the files live entirely under the fork's own "
        "namespace and nothing has to be patched upstream to make them work.",
    ),
    (
        "candidate",
        "Worth taking with a known cost",
        "The gameplay claim held up under verification, but grafting it means editing upstream "
        "files, which is rebase tax on every future sync.",
    ),
)


def rival_table(feature: dict, entries: list[dict]) -> list[str]:
    """Competing forks side by side, best-verified first.

    Several forks building the same thing is a comparison, not a repetition:
    the decision is which version to take, and that needs them in one table.
    """
    files = {row["fork"]: row.get("files", 0) for row in cat.authors(feature)}
    lines = [
        f"{len(entries)} forks built this; the assessment above is the best-verified one.",
        "",
        "| Fork | Files | Graft shape | Verified | Checked claims |",
        "| --- | --- | --- | --- | --- |",
    ]
    for entry in entries:
        check = cat.verdict(entry) or {}
        fork = (entry.get("covers") or ["?"])[0]
        extra = f" +{len(entry['covers']) - 1}" if len(entry.get("covers") or []) > 1 else ""
        over = len(check.get("overclaims") or [])
        lines.append(
            f"| {fork}{extra} | {files.get(fork, '?')} "
            f"| {SHAPE_LABEL.get(entry['graft_shape'], entry['graft_shape'])} "
            f"| {check.get('gameplay_supported', '?').replace('-', ' ')} "
            f"| {f'{over} overclaim(s)' if over else 'clean'} |"
        )
    return lines + [""]


def shortlist_section(data: dict) -> list[str]:
    """The catalog's answer to "what should we graft first".

    Membership requires a verifier to have tried and failed to knock the unit
    down. That is why it is short, and why nothing gets in on a strong-reading
    but unchecked assessment.
    """
    rows = cat.shortlist(data)
    grouped = cat.shortlist_by_feature(data)
    if not rows:
        return []
    total = sum(len(cat.assessments(f)) for f in data["features"])
    lines = [
        "## Shortlist",
        "",
        f"{len(grouped)} features ({len(rows)} fork implementations) of {total} assessed,",
        "through two narrow filters. Every one was",
        "re-read by a second agent whose job was to refute it, and nothing gets in on an",
        "unverified claim. On top of that a feature has to be somewhere this server wants to",
        "go, which is judgement and lives in `fit.yml` rather than being inferred.",
        "",
    ]
    for tier, title, blurb in TIERS:
        picked = [(f, es) for f, es in grouped if cat.graft_tier(es[0]) == tier]
        if not picked:
            continue
        lines += [f"### {title}", "", blurb, ""]
        for feature, entries in picked:
            lines += assessment(feature, entries[0], with_name=True)
            if len(entries) > 1:
                lines += rival_table(feature, entries)

    ruled = cat.rejected(data)
    if ruled:
        lines += [
            "### Ruled out by direction",
            "",
            "These passed verification and were still turned down. Kept visible with the reason,",
            'because "we considered that and said no" is what gets lost and re-proposed later.',
            "",
        ]
        lines += [
            f"- [{f['name']}](#{f['id']}) — {f.get('fit_why', '')}" for f in ruled
        ] + [""]
    return lines


def render(data: dict) -> str:
    forks = data["forks"]
    features = data["features"]
    categories = cat.categories(data)
    columns = [f for f in forks if f["id"] != "wizden"]
    stats = cat.stats(data)

    lines = [
        "<!--",
        "SPDX-FileCopyrightText: honksquad-ss14 contributors",
        f"{SPDX_TAG}: AGPL-3.0-or-later",
        "-->",
        "",
        "# SS14 fork feature catalog",
        "",
        "What each fork in the ecosystem built on top of Wizard's Den, where it lives in their",
        f"tree, and — for the {stats['assessed']} units assessed so far — what it does to a round.",
        "A row says a fork *has* the feature; only the shortlist says anything about taking it.",
        "",
        f"{stats['forks']} forks · {stats['features']} features · "
        f"{stats['authors']} authored · {stats['carriers']} vendored from another fork · "
        f"{stats['assessed']} assessed · {stats['shortlist']} verified takes",
        "",
        f"Portability is measured, not assumed. An independent pass re-read {stats['verified']}",
        f"assessments trying to refute them and corrected the graft shape of {stats['corrected']},",
        "including eight of the ten features first judged self-contained. The cause is a limit of",
        "the path data: it indexes the directories a fork *claims*, so it cannot see upstream files",
        "a fork edits in place. Treat an unverified portability claim as a hypothesis.",
        "",
        f"{OWN} the fork authored it &nbsp;&nbsp; {VENDORED} the fork vendored it from another fork",
        "",
        "Generated by `Tools/graft/build_markdown.py`. Edit `fork-features.yml`, not this file.",
        "",
        *shortlist_section(data),
        "## Forks",
        "",
        *fork_table(forks),
        "",
        "## Feature matrix",
        "",
    ]

    for category in categories:
        if any(f["category"] == category for f in features):
            lines += [f"### {category.title()}", "", *matrix(features, columns, category), ""]

    lines += ["## Features", ""]
    for category in categories:
        rows = [f for f in features if f["category"] == category]
        if not rows:
            continue
        lines += [f"## {category.title()}", ""]
        for feature in rows:
            lines += detail(feature)

    unmapped = data.get("unmapped") or []
    if unmapped:
        lines += [
            "## Unmapped",
            "",
            "Fork features that fit no canonical entry yet. Fold them in or drop them as the",
            "taxonomy settles.",
            "",
            "| Fork | Feature | Why |",
            "| --- | --- | --- |",
        ]
        for item in unmapped:
            lines.append(
                f"| {item.get('fork', '')} | {item.get('name', '')} | {item.get('why', '')} |"
            )
        lines.append("")

    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--yaml", type=Path, default=cat.CATALOG)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--check", action="store_true", help="fail if the Markdown is stale")
    opts = parser.parse_args()

    rendered = render(cat.load(opts.yaml))

    if opts.check:
        current = opts.out.read_text(encoding="utf-8") if opts.out.exists() else ""
        if current != rendered:
            print(
                f"{opts.out} is out of date — run: python3 Tools/graft/build_markdown.py",
                file=sys.stderr,
            )
            return 1
        print(f"{opts.out} is up to date")
        return 0

    opts.out.write_text(rendered, encoding="utf-8")
    print(f"wrote {opts.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
