/* SPDX-FileCopyrightText: honksquad-ss14 contributors
   SPDX-License-Identifier: AGPL-3.0-or-later */

/* Phase 2 gameplay assessments: what a feature does to a round, and whether
   the catalog can actually stand behind that. Kept apart from catalog.js,
   which stays about who-has-what. */

const IMPACT_ORDER = ["structural", "loop-change", "activity", "cosmetic"];
const SHAPE_ORDER = ["entangled", "needs-upstream-edits", "drop-in"];
const SHAPE_LABEL = {
  "drop-in": "drop-in",
  "needs-upstream-edits": "upstream edits",
  entangled: "entangled",
};

const units = (feature) => feature.assessments || [];
const checked = (unit) => (unit.verified && typeof unit.verified === "object" ? unit.verified : null);

// Only a verified unit earns a tier. Verification refuted 8 of the 10 units
// first called drop-in, so an unverified claim of portability is a hypothesis.
function tier(unit) {
  const v = checked(unit);
  if (!v || v.gameplay_supported === "unsupported") return "";
  if (unit.graft_shape === "drop-in") return "take-now";
  // No refuted specific, and an assessor who was not hedging. The looser
  // "supported and not entangled" rule admitted half the catalog once every
  // unit had a verdict, which is not a recommendation.
  if (
    v.gameplay_supported === "supported" &&
    !(v.overclaims || []).length &&
    unit.confidence === "high" &&
    unit.graft_shape !== "entangled"
  )
    return "candidate";
  return "";
}

// Two independent gates on shortlist membership: the evidence has to hold up,
// and the feature has to be somewhere we want to go. A well-verified graft can
// still be the wrong direction, and that call is the maintainer's, not the
// analysis's.
const eligible = (feature, unit) => Boolean(tier(unit)) && feature.fit !== "rejected";

const FIT_LABEL = {
  wanted: "wanted",
  "partly-taken": "already partly taken",
  deferred: "back burner",
  rejected: "not our direction",
};

// Worst case across a feature's units — a feature is only as portable as its
// least portable assessed take, and rounding that up would flatter it.
const worst = (feature, field, order) =>
  order.find((value) => units(feature).some((u) => u[field] === value)) || "";

function featureBadges(feature) {
  const rows = units(feature);
  if (!rows.length) return "";
  const impact = worst(feature, "round_impact", IMPACT_ORDER);
  const shape = worst(feature, "graft_shape", SHAPE_ORDER);
  const top = rows.map(tier).find(Boolean);
  const listed =
    top && feature.fit !== "rejected" ? `<span class="badge take">shortlisted</span>` : "";
  const fit = feature.fit
    ? `<span class="badge fit ${esc(feature.fit)}" title="${esc(feature.fit_why || "")}">${esc(
        FIT_LABEL[feature.fit] || feature.fit
      )}</span>`
    : "";
  return (
    `<span class="badge impact ${esc(impact)}">${esc(impact)}</span>` +
    `<span class="badge shape ${esc(shape)}">${esc(SHAPE_LABEL[shape] || shape)}</span>${fit}${listed}`
  );
}

function verdictBlock(unit) {
  const v = checked(unit);
  if (!v) {
    return `<p class="a-ver none">Not independently verified. Of the units that were, roughly a
      quarter had their graft shape corrected, so read this as a claim rather than a finding.</p>`;
  }
  const bad = v.gameplay_supported === "partly-supported";
  const over = (v.overclaims || []).length
    ? `<ul class="a-over">${v.overclaims.map((o) => `<li>${esc(o)}</li>`).join("")}</ul>`
    : "";
  const moved = unit.graft_shape_claimed
    ? `<p class="a-moved">Assessed as <b>${esc(SHAPE_LABEL[unit.graft_shape_claimed])}</b>,
       corrected to <b>${esc(SHAPE_LABEL[unit.graft_shape])}</b> by the verifier.</p>`
    : "";
  return `<div class="a-ver ${bad ? "warn" : "ok"}">
    <p class="a-vhead">Verified: ${esc(v.gameplay_supported.replace("-", " "))}${
    v.upstream_reading ? ` · ${esc(v.upstream_reading.replace(/-/g, " "))}` : ""
  }</p>
    ${moved}
    <p class="a-vwhy">${esc(v.why || "")}</p>
    ${over ? `<p class="a-olabel">Claims the evidence did not support:</p>${over}` : ""}
  </div>`;
}

// When several forks built the same thing, the useful output is a comparison,
// not the same card N times. One row per fork: what its verifier concluded and
// what it caught, so "whose version do we take" is answerable at a glance.
function rivals(feature, picked) {
  if (picked.length < 2) return "";
  const rows = picked
    .map((u) => {
      const v = checked(u) || {};
      const fork = (u.covers || [])[0];
      const author = (feature.authors || []).find((a) => a.fork === fork);
      const over = (v.overclaims || []).length;
      return `<tr>
        <td><b>${esc(forkName(fork))}</b>${
        (u.covers || []).length > 1 ? ` +${u.covers.length - 1}` : ""
      }</td>
        <td class="num">${author ? author.files : "—"}</td>
        <td>${esc(SHAPE_LABEL[u.graft_shape] || u.graft_shape)}</td>
        <td>${esc((v.gameplay_supported || "").replace("-", " "))}</td>
        <td class="num">${over ? `${over} overclaim${over > 1 ? "s" : ""}` : "clean"}</td>
      </tr>`;
    })
    .join("");
  return `<div class="rivals scroller"><table>
    <thead><tr><th>Fork</th><th class="num">Files</th><th>Graft shape</th>
    <th>Verified</th><th class="num">Checked claims</th></tr></thead>
    <tbody>${rows}</tbody></table>
    <p class="rivals-note">Ranked best-verified first. "Clean" means a verifier tried to
    refute every specific and could not.</p></div>`;
}

function unitCard(feature, unit, withName) {
  const covers = (unit.covers || [])
    .map((id) => `<span class="ctag" title="${esc(forkName(id))}">${esc(forkTag(id))}</span>`)
    .join("");
  const needs = (unit.depends_on || [])
    .map((id) => `<a href="#f-${esc(id)}">${esc(nameOf(id))}</a>`)
    .join(", ");
  const title = withName
    ? `<h3 class="a-feat"><a href="#f-${esc(feature.id)}">${esc(feature.name)}</a></h3>`
    : "";
  return `<article class="assess">
    ${title}
    <div class="a-head">${covers}
      <span class="badge impact ${esc(unit.round_impact)}">${esc(unit.round_impact)}</span>
      <span class="badge shape ${esc(unit.graft_shape)}">${esc(SHAPE_LABEL[unit.graft_shape])}</span>
      <span class="badge conf">${esc(unit.confidence)} confidence</span>
      ${unit.adoption ? `<span class="badge adopt">${unit.adoption} forks carry it</span>` : ""}
    </div>
    <p class="a-play">${esc(unit.gameplay)}</p>
    <dl class="a-why">
      <dt>Take</dt><dd>${esc(unit.take_because || "")}</dd>
      <dt>Skip</dt><dd>${esc(unit.skip_because || "")}</dd>
    </dl>
    <p class="a-meta">Touches ${(unit.touches || []).map(esc).join(", ") || "—"}${
    needs ? ` · Needs ${needs}` : ""
  }</p>
    ${verdictBlock(unit)}
    <details class="a-ev"><summary>${(unit.evidence || []).length} files read</summary>
      <ul>${(unit.evidence || []).map((p) => `<li><code>${esc(p)}</code></li>`).join("")}</ul>
    </details>
  </article>`;
}

function assessBlock(feature) {
  const rows = units(feature);
  if (!rows.length) return "";
  const note =
    rows.length > 1
      ? `<p class="a-note">${rows.length} forks build this differently enough to assess separately.</p>`
      : "";
  return `<div class="assessments">${note}${rows.map((u) => unitCard(feature, u, false)).join("")}</div>`;
}

// The shortlist is the catalog's actual answer to "what should we graft first",
// and it is short on purpose: everything here survived a verifier told to
// refute it. Nothing unverified is allowed in, however good the claim read.
function renderShortlist() {
  const rank = { "take-now": 0, candidate: 1 };
  const rows = [];
  DATA.features.forEach((f) =>
    units(f).forEach((u) => {
      if (eligible(f, u)) rows.push([f, u]);
    })
  );
  rows.sort(
    (a, b) =>
      (a[0].fit === "wanted" ? 0 : 1) - (b[0].fit === "wanted" ? 0 : 1) ||
      rank[tier(a[1])] - rank[tier(b[1])] ||
      IMPACT_ORDER.indexOf(a[1].round_impact) - IMPACT_ORDER.indexOf(b[1].round_impact) ||
      (b[1].adoption || 0) - (a[1].adoption || 0)
  );
  // Group by feature: three forks building one messenger is one decision with
  // three options, not three entries.
  const byFeature = new Map();
  rows.forEach(([f, u]) => {
    if (!byFeature.has(f.id)) byFeature.set(f.id, [f, []]);
    byFeature.get(f.id)[1].push(u);
  });

  const group = (name, blurb, want) => {
    const picked = [...byFeature.values()].filter(([, us]) => tier(us[0]) === want);
    if (!picked.length) return "";
    return `<section class="tier">
      <h2>${esc(name)} <span class="cat-n">${picked.length}</span></h2>
      <p class="tier-blurb">${blurb}</p>
      ${picked
        .map(
          ([f, us]) =>
            unitCard(f, us[0], true) +
            (us.length > 1
              ? `<div class="alts"><p class="alts-head">${us.length} forks built this — the
                 assessment above is the best-verified one</p>${rivals(f, us)}</div>`
              : "")
        )
        .join("")}
    </section>`;
  };
  const ruled = DATA.features.filter(
    (f) => f.fit === "rejected" && units(f).some((u) => tier(u))
  );
  const ruledOut = ruled.length
    ? `<section class="tier ruled">
        <h2>Ruled out by direction <span class="cat-n">${ruled.length}</span></h2>
        <p class="tier-blurb">These passed verification and were still turned down. Kept
        visible with the reason, because "we already considered that and said no" is exactly
        what gets lost and re-proposed a year later.</p>
        <ul class="ruled-list">${ruled
          .map(
            (f) =>
              `<li><a href="#f-${esc(f.id)}">${esc(f.name)}</a> — ${esc(f.fit_why || "")}</li>`
          )
          .join("")}</ul>
      </section>`
    : "";

  return `<div class="shortlist">
    <p class="notice"><strong>${byFeature.size} features (${rows.length} fork
    implementations), out of 154 assessed.</strong> Two filters,
    both narrow. Every unit below was re-read by a second agent whose job was to refute it,
    and the catalog will not shortlist a feature on an unverified claim, so the other 112
    assessed units are not eligible until someone checks them. On top of that, a feature has
    to be somewhere this server wants to go, which is a judgement call and is recorded in
    <code>fit.yml</code> rather than inferred.</p>
    ${group(
      "Take now",
      `Verified as genuinely self-contained: the files live entirely under the fork's own
       namespace and nothing has to be patched upstream to make them work.`,
      "take-now"
    )}
    ${group(
      "Worth taking with a known cost",
      `The gameplay claim held up under verification, but grafting it means editing upstream
       files, which is rebase tax on every future sync. Worth it for the right feature; know
       the bill before you sign.`,
      "candidate"
    )}
    ${ruledOut}
  </div>`;
}
