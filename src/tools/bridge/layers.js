/**
 * Shared layer-capture for the bridge tools.
 *
 * Both `html_export` (the real Unity sprite) and `html_preview` (the same sprite,
 * shown for approval) call `captureLayerPng` so they can't drift. It screenshots
 * ONE element in isolation → a transparent, shape-accurate PNG buffer:
 *  - hides the element's peeled descendants (text/icon/nested sprite) + dims own-text;
 *  - hides every SIBLING along the ancestor path so a padded capture can't pick up
 *    neighbours;
 *  - clears ancestor backdrops AND drops the target's box-shadow RINGS (outset,
 *    0-blur) which ignore clip-path and left a 1px corner square — while KEEPING
 *    inset shadows (those are the border, clipped to the shape) and, for icons, soft
 *    outset glows;
 *  - for ICONS only, expands the capture by the drop-shadow glow extent so the glow
 *    isn't cut off. Frames (sprites) are NOT padded, so their 9-slice border stays
 *    aligned to the texture edges.
 * Finally it drops near-empty captures (e.g. an animated sheen overlay that renders
 * as almost nothing) so no blank layer is emitted.
 */
import zlib from "node:zlib";

/** Peeled descendant layer selectors (text/icon/nested sprite) to hide. */
function collectChildLayerSelectors(node) {
  const acc = [];
  const visit = (n) => {
    if (!n.children) return;
    for (const c of n.children) {
      if (c.ownText) { visit(c); continue; }
      if ((c.kind === "text" || c.kind === "icon" || c.kind === "sprite") && c.selector) acc.push(c.selector);
      visit(c);
    }
  };
  visit(node);
  return acc;
}

/** Own-text label selectors (share the frame selector) to dim to transparent. */
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
 * Isolate `targetSel`'s element for a clean screenshot and return capture geometry.
 * Runs in the browser; saves each touched element's inline style on window.__uRestore.
 *
 * @param {{targetSel:string, hideSels:string[], dimSels:string[], isIcon:boolean}} a
 * @returns {null | {x:number,y:number,w:number,h:number,margin:number,docW:number,docH:number}}
 */
const SETUP_FN = ({ targetSel, hideSels, dimSels, isIcon }) => {
  const R = (window.__uRestore = []);
  // Save each element's ORIGINAL inline style ONCE. An element can be touched by
  // multiple steps (e.g. a nested sprite that is both hidden AND has its own-text
  // dimmed, since own-text shares the frame selector); saving again would capture
  // the already-mutated style, and RESTORE_FN would then re-apply it — leaving the
  // element hidden for its own later capture (the "badge came out empty" bug).
  const seen = new Set();
  const save = (el) => { if (seen.has(el)) return; seen.add(el); R.push([el, el.getAttribute("style")]); };

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

  // Hide siblings along the path to <body> so a padded capture can't catch neighbours.
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

  // Clear ancestor backdrops (background + border/outline color + box-shadow).
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
  let m = 0; // outward glow extent → capture margin (icons only)

  // Box-shadow on the target, per layer:
  //  - inset → the BORDER (clipped to the shape) → KEEP.
  //  - outset 0-blur → a hard rectangular RING (ignores clip-path) → DROP.
  //  - outset blur>0 → a soft glow → KEEP for icons (and pad); DROP for sprites
  //    (a frame can't be padded without misaligning its 9-slice border).
  const splitTop = (str) => {
    const out = []; let depth = 0, cur = "";
    for (const ch of str) {
      if (ch === "(") depth++; else if (ch === ")") depth--;
      if (ch === "," && depth === 0) { out.push(cur); cur = ""; } else cur += ch;
    }
    if (cur.trim()) out.push(cur);
    return out.map((x) => x.trim()).filter(Boolean);
  };
  const numsPx = (str) => (str.replace(/\b(?:rgba?|hsla?)\([^)]*\)/g, "").match(/-?\d*\.?\d+px/g) || []).map(parseFloat);
  const bs = cs.boxShadow || "";
  if (bs && bs !== "none") {
    const kept = [];
    for (const layer of splitTop(bs)) {
      if (/\binset\b/.test(layer)) { kept.push(layer); continue; } // border → keep
      const ns = numsPx(layer);
      const blur = ns[2] || 0, spread = ns[3] || 0;
      const ox = Math.abs(ns[0] || 0), oy = Math.abs(ns[1] || 0);
      if (isIcon && blur > 0) { kept.push(layer); m = Math.max(m, Math.max(ox, oy) + blur + Math.max(0, spread)); }
      // else drop (hard ring; or soft glow on a sprite)
    }
    save(t);
    t.style.setProperty("box-shadow", kept.length ? kept.join(", ") : "none", "important");
  }

  // drop-shadow filter glow → pad icons only (keep frame 9-slice aligned).
  if (isIcon) {
    const filterNoColor = (cs.filter || "").replace(/\b(?:rgba?|hsla?)\([^)]*\)/g, "");
    const re = /drop-shadow\(([^)]*)\)/g;
    let mt;
    while ((mt = re.exec(filterNoColor))) {
      const ns = (mt[1].match(/-?\d*\.?\d+px/g) || []).map(parseFloat);
      const ox = Math.abs(ns[0] || 0), oy = Math.abs(ns[1] || 0), bl = ns[2] || 0;
      m = Math.max(m, Math.max(ox, oy) + bl);
    }
  }

  const b = t.getBoundingClientRect();
  return {
    x: b.left, y: b.top, w: b.width, h: b.height,
    margin: m > 0 ? Math.ceil(m) + 2 : 0, // +2px AA buffer when padding
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
 * True if an RGBA8 PNG buffer has more than a trivial amount of opaque content.
 * Decodes via zlib (no deps) and counts pixels with alpha above a threshold. On any
 * parse error it returns true (keep the layer — fail safe).
 *
 * @param {Buffer} buf - the PNG bytes.
 * @param {number} [alphaThresh] - alpha above this counts as "painted" (default 16).
 * @param {number} [minFrac] - min painted fraction to be "non-empty" (default 0.004).
 * @returns {boolean}
 */
function pngHasContent(buf, alphaThresh = 16, minFrac = 0.004) {
  try {
    if (!buf || buf.length < 33) return false;
    let off = 8, width = 0, height = 0, bitDepth = 0, colorType = 0;
    const idat = [];
    while (off + 8 <= buf.length) {
      const len = buf.readUInt32BE(off);
      const type = buf.toString("ascii", off + 4, off + 8);
      const start = off + 8;
      if (type === "IHDR") {
        width = buf.readUInt32BE(start);
        height = buf.readUInt32BE(start + 4);
        bitDepth = buf[start + 8];
        colorType = buf[start + 9];
      } else if (type === "IDAT") {
        idat.push(buf.subarray(start, start + len));
      } else if (type === "IEND") break;
      off = start + len + 4; // data + CRC
    }
    if (colorType !== 6 || bitDepth !== 8 || !width || !height) return true; // unknown → keep
    const raw = zlib.inflateSync(Buffer.concat(idat));
    const stride = width * 4;
    const out = Buffer.alloc(height * stride);
    for (let y = 0; y < height; y++) {
      const rowStart = y * (stride + 1);
      if (rowStart >= raw.length) break;
      const filter = raw[rowStart];
      const src = rowStart + 1, dst = y * stride;
      for (let x = 0; x < stride; x++) {
        const rv = raw[src + x] || 0;
        const a = x >= 4 ? out[dst + x - 4] : 0;
        const b = y > 0 ? out[dst - stride + x] : 0;
        const c = x >= 4 && y > 0 ? out[dst - stride + x - 4] : 0;
        let v;
        switch (filter) {
          case 1: v = rv + a; break;
          case 2: v = rv + b; break;
          case 3: v = rv + ((a + b) >> 1); break;
          case 4: {
            const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
            v = rv + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
            break;
          }
          default: v = rv;
        }
        out[dst + x] = v & 0xff;
      }
    }
    const total = width * height;
    let opaque = 0;
    for (let i = 3; i < out.length; i += 4) if (out[i] > alphaThresh) opaque++;
    return opaque / total >= minFrac;
  } catch {
    return true; // fail safe: keep the layer
  }
}

/**
 * Screenshot one layer node in isolation → a transparent, shape-accurate, (for icons)
 * glow-inclusive PNG buffer. Returns null if the element is missing OR the capture is
 * near-empty (e.g. an animated sheen). Optionally runs `opts.drawOverlay` after
 * isolation (e.g. the preview's 9-slice guides) before the shot.
 *
 * @param {import("playwright").Page} page - the loaded page.
 * @param {object} node - the layer node (needs selector, kind, children).
 * @param {{ drawOverlay?: () => Promise<void> }} [opts] - optional pre-shot hook.
 * @returns {Promise<Buffer|null>}
 */
export async function captureLayerPng(page, node, opts = {}) {
  const hideSels = collectChildLayerSelectors(node);
  const dimSels = collectOwnTextSelectors(node);
  const geo = await page.evaluate(SETUP_FN, {
    targetSel: node.selector,
    hideSels,
    dimSels,
    isIcon: node.kind === "icon",
  });
  if (!geo) {
    await page.evaluate(RESTORE_FN);
    return null;
  }
  try {
    if (opts.drawOverlay) await opts.drawOverlay();
    let x = geo.x - geo.margin, y = geo.y - geo.margin;
    let w = geo.w + geo.margin * 2, h = geo.h + geo.margin * 2;
    if (x < 0) { w += x; x = 0; }
    if (y < 0) { h += y; y = 0; }
    if (x + w > geo.docW) w = geo.docW - x;
    if (y + h > geo.docH) h = geo.docH - y;
    if (w <= 0 || h <= 0) return null;
    const buf = await page.screenshot({
      clip: { x, y, width: w, height: h },
      omitBackground: true,
      animations: "disabled",
      caret: "hide",
    });
    return pngHasContent(buf) ? buf : null; // drop near-empty captures
  } finally {
    await page.evaluate(RESTORE_FN);
  }
}
