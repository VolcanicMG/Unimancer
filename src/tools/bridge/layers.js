/**
 * Shared page-isolation helpers for the bridge tools.
 *
 * Both `html_export` (to rasterize a layer's true sprite) and `html_preview` (to
 * show that same sprite) need to screenshot ONE element so the PNG contains only
 * that element — no baked-in child text/icons, and TRANSPARENT everywhere the
 * element doesn't paint (outside a clip-path bevel, beyond rounded corners). Keeping
 * this here means preview and export can't drift.
 *
 * `withIsolatedElement` does two things while `fn` runs, then restores everything:
 *  1. Hides the element's peeled descendant layers (text / icon / nested sprite) via
 *     `visibility:hidden` — keeps layout (so 9-slice geometry is unchanged) but keeps
 *     them out of the raster. Own-text labels share the frame selector, so they're
 *     dimmed to transparent instead (hiding would blank the frame).
 *  2. Clears the BACKGROUND of every ANCESTOR (up the parent chain incl. html/body).
 *     Without this, a screenshot with omitBackground still shows the dark ancestor
 *     background through the element's clipped/rounded corners (the "black corners"
 *     bug) — clearing ancestor backdrops lets those corners screenshot as transparent
 *     so the sprite keeps its real shape.
 */

/**
 * Run `fn` with `node`'s element isolated for a clean, shape-accurate transparent
 * screenshot: peeled descendants hidden + ancestor backgrounds cleared. Always
 * restores the page afterward, even if `fn` throws.
 *
 * @param {import("playwright").Page} page - the loaded page.
 * @param {object} node - the sprite/icon node being screenshot (has selector + children).
 * @param {() => Promise<void>} fn - the screenshot action to run while isolated.
 * @returns {Promise<void>}
 */
export async function withIsolatedElement(page, node, fn) {
  const hideSelectors = collectChildLayerSelectors(node);
  const dimSelectors = collectOwnTextSelectors(node);
  await page.evaluate(({ sels, dims, targetSel }) => {
    window.__uHidden = [];
    window.__uDimmed = [];
    window.__uBg = [];
    for (const sel of sels) {
      const el = document.querySelector(sel);
      if (!el) continue;
      window.__uHidden.push([sel, el.style.visibility]);
      el.style.visibility = "hidden";
    }
    for (const sel of dims) {
      const el = document.querySelector(sel);
      if (!el) continue;
      window.__uDimmed.push([sel, el.style.color, el.style.textShadow]);
      el.style.color = "transparent";
      el.style.textShadow = "none";
    }
    // Clear ancestor backdrops so the element's clipped/rounded corners screenshot
    // as transparent instead of showing the dark page/panel background behind them.
    const t = document.querySelector(targetSel);
    if (t) {
      let p = t.parentElement;
      while (p) {
        window.__uBg.push([p, p.style.background, p.style.boxShadow]);
        p.style.setProperty("background", "transparent", "important");
        p.style.setProperty("box-shadow", "none", "important");
        p = p.parentElement;
      }
    }
  }, { sels: hideSelectors, dims: dimSelectors, targetSel: node.selector });
  try {
    await fn();
  } finally {
    await page.evaluate(() => {
      for (const [sel, prev] of window.__uHidden || []) {
        const el = document.querySelector(sel);
        if (el) el.style.visibility = prev || "";
      }
      for (const [sel, c, ts] of window.__uDimmed || []) {
        const el = document.querySelector(sel);
        if (el) { el.style.color = c || ""; el.style.textShadow = ts || ""; }
      }
      for (const [el, bg, bs] of window.__uBg || []) {
        if (el) { el.style.background = bg || ""; el.style.boxShadow = bs || ""; }
      }
      delete window.__uHidden;
      delete window.__uDimmed;
      delete window.__uBg;
    });
  }
}

/**
 * Gather the selectors of all direct & nested child layers that are peeled out
 * (text, icon, or a nested sprite frame) — everything to hide when screenshotting
 * a frame so it captures ONLY itself. The frame's own selector is NOT included.
 *
 * @param {object} node - the frame node.
 * @returns {string[]} selectors to hide.
 */
export function collectChildLayerSelectors(node) {
  const acc = [];
  const visit = (n) => {
    if (!n.children) return;
    for (const c of n.children) {
      // Own-text labels share the frame selector — never visibility-hide them
      // (that would blank the frame); they're dimmed via collectOwnTextSelectors.
      if (c.ownText) { visit(c); continue; }
      if ((c.kind === "text" || c.kind === "icon" || c.kind === "sprite") && c.selector) acc.push(c.selector);
      visit(c);
    }
  };
  visit(node);
  return acc;
}

/**
 * Gather selectors of own-text label layers (text that lives directly on a styled
 * frame, sharing its selector). These are dimmed to transparent during a frame
 * screenshot so the label doesn't bake into the frame raster.
 *
 * @param {object} node - the frame node.
 * @returns {string[]} selectors to dim.
 */
export function collectOwnTextSelectors(node) {
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
