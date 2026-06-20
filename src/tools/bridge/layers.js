/**
 * Shared layer-capture for the bridge tools.
 *
 * Both `html_export` (the real Unity sprite) and `html_preview` (the same sprite,
 * shown for approval) need to screenshot ONE element so the PNG contains only that
 * element — its content children removed, transparent where it doesn't paint, and
 * INCLUDING any outer glow that overflows its box. `captureLayerPng` centralizes
 * this so preview and export can't drift.
 *
 * To get a clean, glow-inclusive, shape-accurate crop it temporarily:
 *  - hides the element's peeled descendant layers (text/icon/nested sprite) and dims
 *    its own-text labels (they share the frame selector);
 *  - hides every SIBLING along the ancestor path, so a PADDED capture region (needed
 *    to include the glow) can't pick up neighbouring widgets;
 *  - clears ancestor backgrounds/borders/box-shadows AND the target's own box-shadow
 *    — a box-shadow ring (e.g. `0 0 0 1.5px rgba(...)`) is rectangular and does NOT
 *    follow a clip-path, so it left a faint 1px square in the clipped corners;
 *  - keeps the target's `filter` (drop-shadow GLOW) and expands the screenshot clip
 *    by the glow's extent so it is not cut off.
 * Everything is restored afterward via a saved inline-style snapshot per element.
 */

/**
 * Selectors of all peeled descendant layers (text/icon/nested sprite) to hide so a
 * frame screenshots as itself only. The frame's own selector is NOT included.
 * @param {object} node - the frame node.
 * @returns {string[]} selectors to hide.
 */
function collectChildLayerSelectors(node) {
  const acc = [];
  const visit = (n) => {
    if (!n.children) return;
    for (const c of n.children) {
      if (c.ownText) { visit(c); continue; } // own-text is dimmed, not hidden
      if ((c.kind === "text" || c.kind === "icon" || c.kind === "sprite") && c.selector) acc.push(c.selector);
      visit(c);
    }
  };
  visit(node);
  return acc;
}

/**
 * Selectors of own-text labels (text directly on a styled frame, sharing its
 * selector) to dim to transparent during the frame screenshot.
 * @param {object} node - the frame node.
 * @returns {string[]} selectors to dim.
 */
function collectOwnTextSelectors(node) {
  const acc = [];
  const visit = (n) => {
    if (!n.children) return;
    for (const c of n.children) {
      if (c.ownText && c.selector) acc.push(c.selector);
      visit(c);
    }
  };
  visit(node);
  return acc;
}

/**
 * Set up the page so `targetSel`'s element can be screenshotted in isolation, and
 * return the capture geometry. Runs in the browser. Saves every touched element's
 * inline `style` attribute on `window.__uRestore` for `RESTORE_FN`.
 *
 * @param {{targetSel:string, hideSels:string[], dimSels:string[]}} a - isolation inputs.
 * @returns {null | {x:number,y:number,w:number,h:number,margin:number,docW:number,docH:number}}
 */
const SETUP_FN = ({ targetSel, hideSels, dimSels }) => {
  const R = (window.__uRestore = []);
  const save = (el) => R.push([el, el.getAttribute("style")]);

  for (const sel of hideSels) {
    const el = document.querySelector(sel);
    if (!el) continue;
    save(el);
    el.style.setProperty("visibility", "hidden", "important");
  }
  for (const sel of dimSels) {
    const el = document.querySelector(sel);
    if (!el) continue;
    save(el);
    el.style.setProperty("color", "transparent", "important");
    el.style.setProperty("text-shadow", "none", "important");
  }

  const t = document.querySelector(targetSel);
  if (!t) return null;

  // Hide every sibling along the path to <body>, so a padded capture (for the glow)
  // doesn't catch neighbouring widgets. visibility:hidden keeps layout intact.
  let node = t;
  while (node && node.parentElement) {
    const parent = node.parentElement;
    for (const sib of Array.from(parent.children)) {
      if (sib === node) continue;
      save(sib);
      sib.style.setProperty("visibility", "hidden", "important");
    }
    if (parent === document.body) break;
    node = parent;
  }

  // Clear ancestor backdrops (background + border + outline + box-shadow). Use
  // *-color:transparent for border/outline so widths stay and layout doesn't shift.
  let p = t.parentElement;
  while (p) {
    save(p);
    p.style.setProperty("background", "transparent", "important");
    p.style.setProperty("border-color", "transparent", "important");
    p.style.setProperty("outline-color", "transparent", "important");
    p.style.setProperty("box-shadow", "none", "important");
    p = p.parentElement;
  }

  const cs = getComputedStyle(t);
  let m = 0; // outward extent of kept glows → the capture margin

  // Box-shadow on the target, layer by layer:
  //  - INSET shadows are the element's BORDER (drawn inside, clipped by clip-path so
  //    they follow the shape) → KEEP.
  //  - OUTSET 0-blur shadows are hard rectangular RINGS that ignore clip-path → the
  //    corner-square artifact → DROP.
  //  - OUTSET shadows with blur > 0 are soft glows → KEEP (and pad the capture).
  const splitTop = (str) => {
    const out = []; let depth = 0, cur = "";
    for (const ch of str) {
      if (ch === "(") depth++; else if (ch === ")") depth--;
      if (ch === "," && depth === 0) { out.push(cur); cur = ""; } else cur += ch;
    }
    if (cur.trim()) out.push(cur);
    return out.map((x) => x.trim()).filter(Boolean);
  };
  const shadowNums = (layer) =>
    (layer.replace(/\b(?:rgba?|hsla?)\([^)]*\)/g, "").match(/-?\d*\.?\d+px/g) || []).map(parseFloat);
  const bs = cs.boxShadow || "";
  if (bs && bs !== "none") {
    const kept = [];
    for (const layer of splitTop(bs)) {
      const ns = shadowNums(layer);
      const blur = ns[2] || 0, spread = ns[3] || 0;
      const ox = Math.abs(ns[0] || 0), oy = Math.abs(ns[1] || 0);
      if (/\binset\b/.test(layer)) { kept.push(layer); continue; } // border → keep
      if (blur <= 0) continue;                                       // hard ring → drop
      kept.push(layer);                                              // soft glow → keep
      m = Math.max(m, Math.max(ox, oy) + blur + Math.max(0, spread));
    }
    save(t);
    t.style.setProperty("box-shadow", kept.length ? kept.join(", ") : "none", "important");
  }

  // Add the drop-shadow filter glow extent to the margin. Strip color functions first
  // so their inner ")" don't truncate the match (drop-shadow(rgb(..) 0 0 6px)).
  const filterNoColor = (cs.filter || "").replace(/\b(?:rgba?|hsla?)\([^)]*\)/g, "");
  const re = /drop-shadow\(([^)]*)\)/g;
  let mt;
  while ((mt = re.exec(filterNoColor))) {
    const ns = (mt[1].match(/-?\d*\.?\d+px/g) || []).map(parseFloat);
    const ox = Math.abs(ns[0] || 0), oy = Math.abs(ns[1] || 0), bl = ns[2] || 0;
    m = Math.max(m, Math.max(ox, oy) + bl);
  }

  const b = t.getBoundingClientRect();
  return {
    x: b.left, y: b.top, w: b.width, h: b.height,
    margin: Math.ceil(m) + 2, // +2px buffer for anti-aliasing
    docW: document.documentElement.clientWidth,
    docH: document.documentElement.clientHeight,
  };
};

/** Restore every element touched by SETUP_FN and remove any 9-slice overlay. */
const RESTORE_FN = () => {
  for (const [el, style] of window.__uRestore || []) {
    if (style === null) el.removeAttribute("style");
    else el.setAttribute("style", style);
  }
  const ov = document.getElementById("__unimancer_ov");
  if (ov) ov.remove();
  delete window.__uRestore;
};

/**
 * Screenshot one layer node in isolation → a transparent, shape-accurate, glow-
 * inclusive PNG buffer. Optionally runs `opts.drawOverlay` after isolation (e.g. the
 * preview's 9-slice guides) before the shot.
 *
 * @param {import("playwright").Page} page - the loaded page.
 * @param {object} node - the layer node (needs selector, kind, children).
 * @param {{ drawOverlay?: () => Promise<void> }} [opts] - optional pre-shot hook.
 * @returns {Promise<Buffer|null>} the PNG buffer, or null if the element was missing.
 */
export async function captureLayerPng(page, node, opts = {}) {
  const hideSels = collectChildLayerSelectors(node);
  const dimSels = collectOwnTextSelectors(node);
  const geo = await page.evaluate(SETUP_FN, { targetSel: node.selector, hideSels, dimSels });
  if (!geo) {
    await page.evaluate(RESTORE_FN);
    return null;
  }
  try {
    if (opts.drawOverlay) await opts.drawOverlay();
    // Expand by the glow margin, then clamp to the page so the clip stays valid.
    let x = geo.x - geo.margin, y = geo.y - geo.margin;
    let w = geo.w + geo.margin * 2, h = geo.h + geo.margin * 2;
    if (x < 0) { w += x; x = 0; }
    if (y < 0) { h += y; y = 0; }
    if (x + w > geo.docW) w = geo.docW - x;
    if (y + h > geo.docH) h = geo.docH - y;
    if (w <= 0 || h <= 0) return null;
    return await page.screenshot({
      clip: { x, y, width: w, height: h },
      omitBackground: true,
      animations: "disabled",
      caret: "hide",
    });
  } finally {
    await page.evaluate(RESTORE_FN);
  }
}
