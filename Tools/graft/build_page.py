#!/usr/bin/env python3
# SPDX-FileCopyrightText: honksquad-ss14 contributors
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Render fork-features.yml into one self-contained HTML page.

Data, styles and script are all inlined, so the output works as a local file and
on GitHub Pages with no build step, no bundler and no CDN request.

  python3 Tools/graft/build_page.py                  # -> Tools/graft/site/index.html
  python3 Tools/graft/build_page.py --out /tmp/x.html
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import catalog as cat

ASSETS = cat.HERE / "assets"
DEFAULT_OUT = cat.HERE / "site" / "index.html"


def payload(data: dict) -> dict:
    forks = data["forks"]
    for fork in forks:
        fork["tag"] = cat.tag(fork)
        fork.setdefault("pass", "round-1")
    return {
        "forks": forks,
        "features": data["features"],
        "categories": cat.categories(data),
        "unmapped": data.get("unmapped", []),
    }


def render(data: dict) -> str:
    body = payload(data)
    stats = cat.stats(data)
    css = (ASSETS / "catalog.css").read_text(encoding="utf-8")
    # assess.js first: catalog.js calls into it as soon as it runs `render()`.
    js = "\n".join((ASSETS / name).read_text(encoding="utf-8") for name in ("assess.js", "catalog.js"))

    # `</` inside a script literal would close the tag early.
    blob = json.dumps(body, separators=(",", ":")).replace("</", "<\\/")
    cats = "".join(f'<option value="{c}">{c}</option>' for c in body["categories"])
    forks = "".join(
        f'<option value="{f["id"]}">{f["name"]}</option>'
        for f in body["forks"]
        if f["id"] != "wizden"
    )

    impacts = "".join(f'<option value="{v}">{v}</option>' for v in cat.ROUND_IMPACT)
    shapes = "".join(
        f'<option value="{v}">{v.replace("needs-upstream-edits", "upstream edits")}</option>'
        for v in reversed(cat.GRAFT_SHAPE)
    )

    notice = ""
    if stats["round1"] or stats["pending"]:
        notice = f"""  <p class="notice"><strong>Mid-pass data.</strong> A second analysis pass re-mapped
  {stats['refined']} forks against a corrected taxonomy. {stats['round1']} forks still carry the
  coarser first-pass mapping and {stats['pending']} are inventoried but not yet mapped, so
  granularity is not comparable across them — a fork with fewer rows may simply be waiting its
  turn. Each fork card says which pass it is on.</p>"""

    return f"""<title>SS14 fork feature catalog</title>
<style>{css}</style>
<div class="wrap">
<header class="masthead">
  <p class="eyebrow">honksquad-ss14 · graft research</p>
  <h1>What every SS14 fork built</h1>
  <p class="lede">An inventory of the Space Station 14 fork ecosystem: which fork added which
  system, where it lives in their tree, who wrote it first, and — for the {stats['assessed']}
  units assessed so far — what it actually does to a round.</p>
  <div class="stats">
    <div><div class="stat-n">{stats['forks']}</div><div class="stat-l">forks</div></div>
    <div><div class="stat-n">{stats['features']}</div><div class="stat-l">features</div></div>
    <div><div class="stat-n">{stats['authors']}</div><div class="stat-l">authored</div></div>
    <div><div class="stat-n">{stats['carriers']}</div><div class="stat-l">vendored copies</div></div>
    <div><div class="stat-n">{stats['paths']}</div><div class="stat-l">paths indexed</div></div>
    <div><div class="stat-n">{stats['assessed']}</div><div class="stat-l">assessed</div></div>
    <div><div class="stat-n">{stats['shortlist']}</div><div class="stat-l">verified takes</div></div>
  </div>
  <p class="notice"><strong>Portability here is measured, not assumed.</strong> An independent
  pass re-read {stats['verified']} assessments trying to refute them and corrected the graft shape
  of {stats['corrected']}, including eight of the ten features first judged self-contained. The
  cause is a limit of the underlying data: it indexes the directories a fork <em>claims</em>, so it
  cannot see upstream files a fork edits in place. Treat an unverified portability claim as a
  hypothesis, and see <em>Shortlist</em> for what survived checking.</p>
{notice}
</header>

<div class="toolbar">
  <div class="views" role="tablist">
    <button data-view="features" aria-selected="true">Features</button>
    <button data-view="shortlist" aria-selected="false">Shortlist</button>
    <button data-view="matrix" aria-selected="false">Matrix</button>
    <button data-view="forks" aria-selected="false">Forks</button>
  </div>
  <input type="search" id="q" placeholder="Search features, forks, paths, gameplay…" aria-label="Search">
  <select id="cat" aria-label="Category"><option value="">All categories</option>{cats}</select>
  <select id="fork" aria-label="Fork"><option value="">Any fork</option>{forks}</select>
  <select id="impact" aria-label="Round impact"><option value="">Any impact</option>{impacts}</select>
  <select id="shape" aria-label="Graft shape"><option value="">Any graft shape</option>{shapes}</select>
  <label class="toggle" for="carriers"><input type="checkbox" id="carriers"> Show carriers</label>
  <button class="linkbtn" id="fold" type="button">Collapse all</button>
  <span class="count" id="count"></span>
</div>

<div class="legend" id="legend">
  <span><span class="swatch"></span> fork wrote it</span>
  <span><span class="swatch v"></span> vendored from another fork — tick <em>Show carriers</em> for their rows</span>
  <span>click a row for per-fork detail, gameplay and verification</span>
  <span>impact and graft-shape filters only match assessed features ({stats['assessed_features']} of {stats['features']})</span>
</div>

<main id="out"></main>

<footer>
  <p>Built from the GitHub trees of every catalogued fork. Features are clustered from the
  namespace directories each fork claims (<code>_DV</code>, <code>_Goobstation</code>,
  <code>_Funkystation</code>).</p>
  <p>Clustering is path-based, so forks that patch upstream files in place rather than adding
  namespaced directories, the Russian-lineage forks especially, are under-represented until a
  content diff catches those edits. The same blind spot is why a feature's graft shape cannot be
  read off its file paths: a fork's tree shows what it added, never what it changed.</p>
  <p>Gameplay text describes what a player experiences, written from that fork's own locale files,
  guidebook entries and prototypes. There is deliberately no quality or fit score — without
  running the code, a rating out of five would be guesswork wearing a number.</p>
</footer>
</div>
<script>const DATA = {blob};{js}</script>
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--yaml", type=Path, default=cat.CATALOG)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    opts = parser.parse_args()

    data = cat.load(opts.yaml)
    opts.out.parent.mkdir(parents=True, exist_ok=True)
    opts.out.write_text(render(data), encoding="utf-8")

    stats = cat.stats(data)
    print(
        f"wrote {opts.out} ({opts.out.stat().st_size // 1024} KB) — "
        f"{stats['forks']} forks, {stats['features']} features, {stats['impls']} implementations"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
