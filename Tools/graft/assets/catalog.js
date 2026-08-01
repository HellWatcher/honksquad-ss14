/* SPDX-FileCopyrightText: honksquad-ss14 contributors
   SPDX-License-Identifier: AGPL-3.0-or-later */
const $ = (s) => document.querySelector(s);
const byId = Object.fromEntries(DATA.forks.map((f) => [f.id, f]));
const state = { view: "features", q: "", cat: "", fork: "", impact: "", shape: "", showCarriers: false };
const nameById = Object.fromEntries(DATA.features.map((f) => [f.id, f.name]));
const nameOf = (id) => nameById[id] || id;

// A feature has authors (forks that wrote it) and carriers (forks shipping
// someone else's copy). Authors are always rows; carriers collapse to one line
// unless asked for, because 680 of 1,483 entries are the same code re-housed.
const authors = (feature) => feature.authors || [];
const carriers = (feature) => feature.carried_by || [];

// Search and the fork filter still have to see carriers even when they are not
// rendered — otherwise picking a fork hides features it demonstrably ships.
const everyone = (feature) => [...authors(feature), ...carriers(feature)];

const forkTag = (id) => (byId[id] ? byId[id].tag : id.toUpperCase());
const forkName = (id) => (byId[id] ? byId[id].name : id);

function matches(feature) {
  const all = everyone(feature);
  if (state.cat && feature.category !== state.cat) return false;
  if (state.fork && !all.some((i) => i.fork === state.fork)) return false;
  // An assessment filter is a question about gameplay, so a feature nobody has
  // assessed yet has no answer and drops out rather than defaulting to a match.
  if (state.impact && !units(feature).some((u) => u.round_impact === state.impact)) return false;
  if (state.shape && !units(feature).some((u) => u.graft_shape === state.shape)) return false;
  if (!state.q) return true;
  const q = state.q.toLowerCase();
  if (feature.name.toLowerCase().includes(q)) return true;
  if ((feature.summary || "").toLowerCase().includes(q)) return true;
  if ((feature.aliases || []).some((a) => a.toLowerCase().includes(q))) return true;
  // Gameplay prose is searchable: "what has a fork built for botany" is the
  // question this catalog exists to answer, and path names cannot answer it.
  if (units(feature).some((u) => (u.gameplay || "").toLowerCase().includes(q))) return true;
  return all.some(
    (i) =>
      forkName(i.fork).toLowerCase().includes(q) ||
      (i.local_name || "").toLowerCase().includes(q) ||
      (i.paths || []).some((p) => p.toLowerCase().includes(q))
  );
}

const esc = (s) =>
  String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

function upstreamBadge(feature) {
  const map = { core: ["ships", "wizden ships it"], partial: ["partial", "wizden partial"] };
  const [cls, label] = map[feature.upstream] || ["", "not upstream"];
  return `<span class="badge ${cls}">${label}</span>`;
}

function chips(feature) {
  const rows = state.showCarriers ? everyone(feature) : authors(feature);
  return rows
    .map((i) => {
      const cls = ["chip", i.via ? "vendored" : "", state.fork === i.fork ? "hit" : ""]
        .filter(Boolean)
        .join(" ");
      const from = i.via ? ` — vendored from ${i.via}` : "";
      return `<span class="${cls}" title="${esc(forkName(i.fork))}: ${i.files} files${esc(from)}">${esc(
        forkTag(i.fork)
      )}</span>`;
    })
    .join("");
}

// A grid, not a table: on a narrow screen the same markup reflows into
// stacked blocks instead of forcing a sideways scroll.
function implRow(i) {
  const fork = byId[i.fork];
  const link = fork
    ? `<a href="${esc(fork.repo)}" target="_blank" rel="noopener">${esc(fork.name)}</a>`
    : esc(forkName(i.fork));
  const via = i.via ? ` <span class="badge via">via ${esc(i.via)}</span>` : "";
  const paths = i.paths || [];
  // Paths stay in the data (search matches them) but are not printed — a
  // dozen path lines per row buried the thing people actually scan for.
  return `<li class="impl${i.via ? " carried" : ""}">
    <span class="i-fork">${link}${via}</span>
    <span class="i-name">${esc(i.local_name || "—")}</span>
    <span class="i-files">${i.files}</span>
    <span class="i-dirs">${paths.length}</span>
    <span class="i-browse">${source(fork, i)}</span>
  </li>`;
}

// Adoption is never hidden: the carriers line states who else ships this even
// with the rows collapsed, because "12 other forks already took it" is the
// strongest signal in the catalog that a graft is safe.
function carriersLine(feature) {
  const rows = carriers(feature);
  if (!rows.length) return "";
  const tags = rows
    .map((i) => `<span class="ctag" title="${esc(forkName(i.fork))} — via ${esc(i.via)}">${esc(forkTag(i.fork))}</span>`)
    .join("");
  return `<p class="carriers">also carried by ${rows.length}: ${tags}</p>`;
}

function detail(feature) {
  const rows = authors(feature);
  const shown = state.showCarriers ? [...rows, ...carriers(feature)] : rows;
  const none = rows.length
    ? ""
    : `<p class="carriers">No fork authored this — every copy is vendored from elsewhere.</p>`;
  const table = shown.length
    ? `<ul class="impls">
    <li class="impl head" aria-hidden="true">
      <span class="i-fork">Fork</span>
      <span class="i-name">Calls it</span>
      <span class="i-files">Files</span>
      <span class="i-dirs">Dirs</span>
      <span class="i-browse"></span>
    </li>
    ${shown.map(implRow).join("")}
  </ul>`
    : "";
  return `<div class="detail">${assessBlock(feature)}${none}${table}${
    state.showCarriers ? "" : carriersLine(feature)
  }</div>`;
}

function source(fork, impl) {
  if (!fork || !fork.repo || !impl.paths.length) return "";
  const branch = fork.branch || "master";
  const target = impl.paths[0];
  const url = `${fork.repo}/tree/${branch}/${target}`;
  const extra = impl.paths.length > 1 ? ` +${impl.paths.length - 1}` : "";
  return `<a class="browse" href="${esc(url)}" target="_blank" rel="noopener"
    title="${esc(target)}">browse${extra} ↗</a>`;
}

function renderFeatures(list) {
  if (!list.length) return `<p class="empty">Nothing matches that.</p>`;
  const cats = DATA.categories.filter((c) => list.some((f) => f.category === c));
  return cats
    .map((cat) => {
      const rows = list.filter((f) => f.category === cat);
      const body = rows
        .map(
          (f) => `<details class="feature" id="f-${esc(f.id)}">
            <summary>
              <div class="f-name">${esc(f.name)}</div>
              <div class="f-sum">${esc(f.summary || "")}</div>
              <div class="f-right"><div class="f-badges">${upstreamBadge(f)}${featureBadges(f)}</div>
              <div class="chips">${chips(f)}</div></div>
            </summary>
            ${detail(f)}
          </details>`
        )
        .join("");
      return `<details class="cat" open>
        <summary><span class="caret"></span><h2>${esc(cat)}</h2><div class="cat-rule"></div>
        <span class="cat-n">${rows.length}</span></summary>${body}</details>`;
    })
    .join("");
}

function renderMatrix(list) {
  if (!list.length) return `<p class="empty">Nothing matches that.</p>`;
  const forks = DATA.forks.filter((f) => f.id !== "wizden");
  const head = forks.map((f) => `<th class="rot"><div>${esc(f.tag)}</div></th>`).join("");
  const rows = list
    .map((f) => {
      // The matrix always shows both roles — an ecosystem view that hid vendored
      // copies would read as "one fork's private toy" for widely-adopted code.
      const has = Object.fromEntries(everyone(f).map((i) => [i.fork, i]));
      const cells = forks
        .map((fork) => {
          const impl = has[fork.id];
          if (!impl) return `<td class="cell">·</td>`;
          const from = impl.via ? ` — via ${impl.via}` : "";
          return `<td class="cell ${impl.via ? "" : "own"}" title="${esc(fork.name)}: ${
            impl.files
          } files${esc(from)}">${impl.via ? "○" : "●"}</td>`;
        })
        .join("");
      return `<tr><td class="row-h">${esc(f.name)}</td>${cells}</tr>`;
    })
    .join("");
  return `<div class="matrix scroller"><table>
    <thead><tr><th class="row-h"></th>${head}</tr></thead><tbody>${rows}</tbody></table></div>`;
}

function renderForks() {
  const counts = {};
  DATA.features.forEach((f) =>
    everyone(f).forEach((i) => {
      counts[i.fork] = counts[i.fork] || { own: 0, vendored: 0 };
      counts[i.fork][i.via ? "vendored" : "own"] += 1;
    })
  );
  return `<div class="forks">${DATA.forks
    .map((f) => {
      const c = counts[f.id] || { own: 0, vendored: 0 };
      const ns = (f.namespaces || []).map(esc).join(" ") || "no namespace, patches upstream dirs";
      return `<article class="fork">
        <h3><a href="${esc(f.repo)}" target="_blank" rel="noopener">${esc(f.name)}</a></h3>
        <div class="ns">${ns} <span class="flag ${f.pass}">${
          f.pass === "refined" ? "refined pass" : f.pass === "unmapped" ? "not yet mapped" : "round-1 pass"
        }</span></div>
        <dl>
          <dt>License</dt><dd>${esc(f.license || "unknown")}</dd>
          <dt>Features</dt><dd>${c.own} own${c.vendored ? `, ${c.vendored} vendored` : ""}</dd>
          <dt>Vendors</dt><dd>${(f.vendors_from || []).slice(0, 10).map(esc).join(" ") || "nothing"}</dd>
          <dt>Lineage</dt><dd>${esc(f.lineage || "")}</dd>
        </dl>
      </article>`;
    })
    .join("")}</div>`;
}

const VIEWS = {
  features: renderFeatures,
  matrix: renderMatrix,
  forks: () => renderForks(),
  shortlist: () => renderShortlist(),
};

function render() {
  const list = DATA.features.filter(matches);
  const authored = list.reduce((n, f) => n + authors(f).length, 0);
  const carried = list.reduce((n, f) => n + carriers(f).length, 0);
  const assessed = list.reduce((n, f) => n + units(f).length, 0);
  $("#count").textContent =
    state.view === "forks"
      ? `${DATA.forks.length} forks`
      : state.view === "shortlist"
      ? "verified takes only"
      : `${list.length} features · ${authored} authored · ${carried} carried · ${assessed} assessed`;
  $("#legend").hidden = state.view !== "features" && state.view !== "matrix";
  $("#fold").hidden = state.view !== "features";
  $("#out").innerHTML = VIEWS[state.view](list);
}

document.querySelectorAll(".views button").forEach((btn) => {
  btn.addEventListener("click", () => {
    state.view = btn.dataset.view;
    document.querySelectorAll(".views button").forEach((b) =>
      b.setAttribute("aria-selected", String(b === btn))
    );
    render();
  });
});

$("#carriers").addEventListener("change", (e) => {
  state.showCarriers = e.target.checked;
  render();
});

$("#fold").addEventListener("click", () => {
  const cats = document.querySelectorAll("details.cat");
  const anyOpen = [...cats].some((c) => c.open);
  cats.forEach((c) => (c.open = !anyOpen));
  $("#fold").textContent = anyOpen ? "Expand all" : "Collapse all";
});

$("#q").addEventListener("input", (e) => {
  state.q = e.target.value.trim();
  render();
});
$("#cat").addEventListener("change", (e) => {
  state.cat = e.target.value;
  render();
});
$("#fork").addEventListener("change", (e) => {
  state.fork = e.target.value;
  render();
});
$("#impact").addEventListener("change", (e) => {
  state.impact = e.target.value;
  render();
});
$("#shape").addEventListener("change", (e) => {
  state.shape = e.target.value;
  render();
});

render();
