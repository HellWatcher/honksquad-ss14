<!--
SPDX-FileCopyrightText: honksquad-ss14 contributors
SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Graft tooling

Everything about taking features from other Space Station 14 forks: what the
ecosystem has built, and what we have taken from it.

Two halves, deliberately separate:

- **The catalog** (`fork-features.yml` and the renderers here) is a *shopping
  list*. It records what every catalogued fork added on top of Wizard's Den and
  where it lives in their tree. Nothing in it is committed to.
- **The ledger** (`graft-manifest.yml`, repo root) is a *record*. It says what
  we actually took, from which donor, at which commit. Grafted files keep their
  own SPDX headers and REUSE resolves them by that header, so the ledger tracks
  provenance and drift, not permission. See ADR 0018.

## The catalog

`fork-features.yml` is the source of truth and is hand-editable. Everything else
is generated from it:

```
python3 Tools/graft/build_markdown.py           # -> FORK-CATALOG.md
python3 Tools/graft/build_markdown.py --check   # CI: fails if the Markdown is stale
python3 Tools/graft/build_page.py               # -> site/index.html
```

`build_page.py` inlines the data, `assets/catalog.css`, `assets/assess.js` and
`assets/catalog.js` into a single file with no external requests, so the same
output works opened locally and served from GitHub Pages.

### Shape

```yaml
forks:
  - id: deltav
    name: Delta-V
    repo: https://github.com/DeltaV-Station/Delta-v
    license: AGPL-3.0
    namespaces: [_DV]          # dirs this fork claims in the tree
    vendors_from: [_EE, _NF]   # other forks' content it carries
    pass: refined              # which analysis pass its mapping is from

features:
  - id: surgery-and-limb-wounds
    category: medical
    upstream: absent           # does Wizard's Den ship it: core | partial | absent
    authors:                   # forks that wrote it
      - fork: rmc14
        local_name: Combat medicine
        paths: [Content.Client/_RMC14/Body, ...]
        files: 615
    carried_by:                # forks shipping someone else's copy
      - fork: einstein-engines
        via: _Shitmed          # free text: the donor namespace, not a fork id
        files: 221
        paths: [Content.Shared/_Shitmed, ...]
    assessments:               # what it does to a round; absent until assessed
      - covers: [rmc14]        # forks this assessment speaks for
        gameplay: >-           # what a PLAYER experiences, no code vocabulary
        round_impact: loop-change    # cosmetic | activity | loop-change | structural
        graft_shape: entangled       # drop-in | needs-upstream-edits | entangled
        take_because: ...
        skip_because: ...
        depends_on: [other-feature-id]
        adoption: 12           # carrier count, counted and never judged
        evidence: [paths actually read]
        verified:              # `false` until an independent pass checks it
          gameplay_supported: partly-supported
          overclaims: ["quoted claim the evidence did not support"]
```

Authors and carriers are separate because they answer different questions. An
author's copy is a design worth reading; a carrier's is the same code under
another roof. Only authors get read in depth, and `carried_by` length is the
catalog's adoption signal, how many other forks already judged that code worth
taking. The page shows author rows by default and folds carriers into one *also
carried by* line; tick **Show carriers** for their rows.

### Assessments, and what an unverified one is worth

An `assessments` block says what a feature does to a round. Forks rarely build
the "same" feature the same way, so divergent authors are assessed separately
rather than averaged into one blurb true of none of them.

There is deliberately **no quality or fit score**. Without running the code a
rating out of five is guesswork wearing a number, and it is the field most
likely to cause a bad graft. `round_impact`, `take_because` and `skip_because`
carry the same decision weight honestly.

`graft_shape` is the field to be careful with. It cannot be read off the path
data, because a fork's tree shows what it *added* and never what it *changed*,
so a feature with no upstream paths may still patch upstream files in place. An
independent pass that re-read assessments trying to refute them corrected the
shape of nine, eight of which had been called self-contained. Hence `verified`:
`false` means nobody checked, which is not the same as checked and fine. The
page's **Shortlist** view admits only verified units for exactly this reason.

### How it was built, and what that misses

Features are clustered from the namespace directories each fork claims (`_DV`,
`_Goobstation`, `_Funkystation`), read from the GitHub trees API. That makes
attribution nearly free and is why the catalog can say who authored a feature
versus who vendored it.

It also has one systematic blind spot worth knowing before trusting a gap: a
fork that patches upstream files **in place** rather than adding namespaced
directories is under-represented, because a tree listing only shows new paths,
not edited ones. The Russian-lineage forks (Corvax, SS220, Sunrise, Backmen)
work this way for much of their content. An absent row for those forks means
"not found by path", not "does not exist".

Per-fork `pass` records how far the analysis got: `refined` mappings were split
against the corrected taxonomy, `round-1` mappings are coarser, and `unmapped`
forks are inventoried but not yet mapped. Row counts are not comparable across
those groups.

## Publishing

`.github/workflows/fork-catalog-pages.yml` rebuilds and deploys the page when
`Tools/graft/**` changes on `release`. It needs Settings → Pages → Source set to
"GitHub Actions" once by a maintainer; until then the build step still runs and
the deploy step is what fails.
