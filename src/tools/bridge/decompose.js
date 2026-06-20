/**
 * Shared HTML->widget decomposition for the bridge tools.
 *
 * Both `html_inventory` (dry run) and `html_export` (writes assets) need the
 * EXACT same parse + classification of a Claude Design HTML mockup into a tree
 * of layers. To guarantee they agree, the DOM walk lives here once and is run
 * inside the headless page via `page.evaluate(BROWSER_DECOMPOSE_SRC, ...)`.
 *
 * The browser-side function is authored as a STRING (`BROWSER_DECOMPOSE_SRC`)
 * because it executes in the page's JS context, not Node's. It returns a plain
 * JSON tree of components -> layers, annotating each layer with: kind
 * (sprite|icon|text|group), format (svg|png), geometry (design-px rect relative
 * to the component root), computed 9-slice border, and inferred anchor. It also
 * records, for export, a stable CSS selector path so the exporter can re-locate
 * each element and toggle sibling visibility.
 *
 * INFERENCE / CONVENTION RULES (Claude Design HTML lacks our custom tags):
 *  - Honor data-* when present: data-ui, data-name, data-layer, data-format,
 *    data-9slice, data-anchor.
 *  - Otherwise infer per the rules encoded in `classify()` below.
 *
 * Geometry note: coordinates are in DESIGN PIXELS. We measure with
 * getBoundingClientRect() (CSS px) and divide by the layout/design ratio so the
 * emitted rects are independent of the actual rendered viewport size. Rects are
 * expressed RELATIVE TO THE COMPONENT ROOT (each component is its own origin).
 */

/**
 * The browser-side decomposition source. Evaluated in-page; receives
 * `(designWidth, designHeight)` and returns the component array. Keep this a
 * pure string of an IIFE-friendly function so Playwright can serialize it.
 *
 * @type {string}
 */
export const BROWSER_DECOMPOSE_SRC = /* js */ `
(function decompose(designWidth, designHeight) {
  "use strict";

  // ---- geometry scaling -------------------------------------------------
  // The page is laid out at some viewport width; design coords are normalized
  // to the requested designWidth/designHeight. If the caller did not pass a
  // design size we fall back to the document's own layout size (ratio 1).
  var docW = document.documentElement.clientWidth || window.innerWidth || 1;
  var docH = document.documentElement.clientHeight || window.innerHeight || 1;
  var dw = designWidth || docW;
  var dh = designHeight || docH;
  var sx = dw / docW;
  var sy = dh / docH;

  /** Round to 2dp to keep rects tidy. */
  function r2(n) { return Math.round(n * 100) / 100; }

  /** Bounding rect of an element in DESIGN px (absolute, page origin). */
  function absRect(el) {
    var b = el.getBoundingClientRect();
    return {
      x: r2((b.left + window.scrollX) * sx),
      y: r2((b.top + window.scrollY) * sy),
      w: r2(b.width * sx),
      h: r2(b.height * sy),
    };
  }

  /** Rect of el relative to a root element's top-left, in design px. */
  function relRect(el, root) {
    var a = absRect(el);
    var rr = absRect(root);
    return { x: r2(a.x - rr.x), y: r2(a.y - rr.y), w: a.w, h: a.h };
  }

  /** Parse a CSS px length string ("12px") to a number; 0 when unparseable. */
  function px(v) { var n = parseFloat(v); return isNaN(n) ? 0 : n; }

  /** True when a computed color string is fully transparent / absent. */
  function transparent(c) {
    if (!c) return true;
    if (c === "transparent" || c === "rgba(0, 0, 0, 0)") return true;
    var m = c.match(/rgba?\\(([^)]+)\\)/);
    if (m) {
      var parts = m[1].split(",").map(function (s) { return s.trim(); });
      if (parts.length === 4 && parseFloat(parts[3]) === 0) return true;
    }
    return false;
  }

  /** Convert a computed rgb/rgba color into #rrggbb (drops alpha for TMP). */
  function toHex(c) {
    var m = c && c.match(/rgba?\\(([^)]+)\\)/);
    if (!m) return "#FFFFFF";
    var p = m[1].split(",").map(function (s) { return parseFloat(s.trim()); });
    function h(n) { var s = Math.max(0, Math.min(255, Math.round(n))).toString(16); return s.length === 1 ? "0" + s : s; }
    return "#" + h(p[0]) + h(p[1]) + h(p[2]);
  }

  /** Does this element directly contain visible (non-whitespace) text? */
  function hasOwnText(el) {
    for (var i = 0; i < el.childNodes.length; i++) {
      var n = el.childNodes[i];
      if (n.nodeType === 3 && n.textContent.trim().length > 0) return true;
    }
    return false;
  }

  /** Collapse + trim an element's full text content. */
  function textOf(el) { return (el.textContent || "").replace(/\\s+/g, " ").trim(); }

  /** Element-child elements only (skips text nodes). */
  function elChildren(el) {
    return Array.prototype.filter.call(el.children, function (c) {
      var cs = getComputedStyle(c);
      // Ignore explicitly hidden subtrees and zero-box decorative nodes.
      if (cs.display === "none" || cs.visibility === "hidden") return false;
      return true;
    });
  }

  // ---- visual signal helpers -------------------------------------------
  function hasBackground(cs) {
    if (!transparent(cs.backgroundColor)) return true;
    if (cs.backgroundImage && cs.backgroundImage !== "none") return true;
    return false;
  }
  function hasBorder(cs) {
    return px(cs.borderTopWidth) > 0 || px(cs.borderRightWidth) > 0 ||
           px(cs.borderBottomWidth) > 0 || px(cs.borderLeftWidth) > 0;
  }
  function hasShadow(cs) { return cs.boxShadow && cs.boxShadow !== "none"; }
  function isInlineSvg(el) { return el.tagName && el.tagName.toLowerCase() === "svg"; }
  function isImg(el) { return el.tagName && el.tagName.toLowerCase() === "img"; }

  // ---- 9-slice computation ---------------------------------------------
  // Auto border per side = ceil(border-radius + border-width + max(0, shadow
  // blur+spread)). Clamp so 2*border < dimension. Only for frame-like layers.
  function parseShadowExtent(boxShadow) {
    if (!boxShadow || boxShadow === "none") return 0;
    // Outer shadow only; grab the largest blur+spread across shadow stacks.
    var max = 0;
    // crude split on "), " to separate stacked shadows; rgba commas are inside ().
    var parts = boxShadow.split(/\\),\\s*/);
    for (var i = 0; i < parts.length; i++) {
      var seg = parts[i];
      if (/inset/.test(seg)) continue; // inset doesn't bleed outward
      var nums = (seg.match(/-?\\d+(?:\\.\\d+)?px/g) || []).map(px);
      // order: offsetX offsetY blur spread
      var blur = nums[2] || 0, spread = nums[3] || 0;
      var ext = blur + spread;
      if (ext > max) max = ext;
    }
    return max;
  }

  function autoNineSlice(el, cs, rect) {
    var radius = Math.max(
      px(cs.borderTopLeftRadius), px(cs.borderTopRightRadius),
      px(cs.borderBottomLeftRadius), px(cs.borderBottomRightRadius)
    );
    var bw = Math.max(
      px(cs.borderTopWidth), px(cs.borderRightWidth),
      px(cs.borderBottomWidth), px(cs.borderLeftWidth)
    );
    var shadow = parseShadowExtent(cs.boxShadow);
    var b = Math.ceil((radius + bw + Math.max(0, shadow)) * Math.max(sx, sy));
    if (b <= 0) return null;
    // Clamp so the center slice stays positive on both axes.
    var maxL = Math.floor((rect.w - 1) / 2);
    var maxT = Math.floor((rect.h - 1) / 2);
    var L = Math.min(b, maxL), T = Math.min(b, maxT);
    if (L <= 0 || T <= 0) return null;
    // Uniform border on all four sides (symmetric frame assumption).
    return [L, T, L, T];
  }

  // ---- anchor inference -------------------------------------------------
  // From an element's position relative to its PARENT box: full-bleed->stretch,
  // pinned edges->that edge, otherwise centered.
  function inferAnchor(el, parentEl, dataAnchor, isText, cs) {
    if (dataAnchor) return dataAnchor;
    if (isText && cs) {
      var ta = cs.textAlign;
      if (ta === "left" || ta === "start") return "left";
      if (ta === "right" || ta === "end") return "right";
      return "center";
    }
    if (!parentEl) return "center";
    var c = absRect(el), p = absRect(parentEl);
    var tol = 2; // px tolerance
    var coversW = (c.x - p.x) <= tol && (p.x + p.w) - (c.x + c.w) <= tol;
    var coversH = (c.y - p.y) <= tol && (p.y + p.h) - (c.y + c.h) <= tol;
    if (coversW && coversH) return "stretch";
    var nearLeft = (c.x - p.x) <= tol;
    var nearRight = (p.x + p.w) - (c.x + c.w) <= tol;
    var nearTop = (c.y - p.y) <= tol;
    var nearBottom = (p.y + p.h) - (c.y + c.h) <= tol;
    // Prefer a single dominant edge; fall back to center.
    if (nearTop && !nearBottom) return "top";
    if (nearBottom && !nearTop) return "bottom";
    if (nearLeft && !nearRight) return "left";
    if (nearRight && !nearLeft) return "right";
    return "center";
  }

  // ---- stable selector path (for the exporter to re-locate elements) ----
  // nth-child chain from <html>, robust to repeated tags/classes.
  function cssPath(el) {
    var parts = [];
    var node = el;
    while (node && node.nodeType === 1 && node.tagName.toLowerCase() !== "html") {
      var idx = 1, sib = node;
      while ((sib = sib.previousElementSibling) != null) idx++;
      parts.unshift(node.tagName.toLowerCase() + ":nth-child(" + idx + ")");
      node = node.parentElement;
    }
    return parts.length ? "html > " + parts.join(" > ") : "html";
  }

  // ---- naming ----------------------------------------------------------
  var nameCounts = {};
  function uniqueName(base) {
    var b = (base || "node").replace(/[^A-Za-z0-9_]+/g, "_").replace(/^_+|_+$/g, "") || "node";
    nameCounts[b] = (nameCounts[b] || 0) + 1;
    return nameCounts[b] === 1 ? b : b + "_" + nameCounts[b];
  }

  // ---- per-element classification --------------------------------------
  // Returns one of: sprite | icon | text | group, honoring data-layer override.
  function classify(el, cs) {
    var override = el.getAttribute("data-layer");
    if (override) {
      // skip is handled by the caller (it prunes the node entirely).
      if (override === "sprite" || override === "icon" || override === "text") return override;
    }
    if (isInlineSvg(el)) return "icon";
    if (isImg(el)) return "icon";
    var kids = elChildren(el);
    if (kids.length === 0) {
      if (hasOwnText(el)) return "text";
      // A childless styled box (e.g. a divider/bar) is a sprite frame.
      if (hasBackground(cs) || hasBorder(cs) || hasShadow(cs)) return "sprite";
      return "group";
    }
    // Has children: a styled container is a frame sprite; otherwise a group.
    if (hasBackground(cs) || hasBorder(cs) || hasShadow(cs)) return "sprite";
    return "group";
  }

  /** Inferred export format for a layer kind + element. */
  function inferFormat(el, kind, dataFormat) {
    if (dataFormat === "svg" || dataFormat === "png") return dataFormat;
    if (isInlineSvg(el)) return "svg"; // keep inline <svg> vector
    return "png";
  }

  // ---- recursive layer build -------------------------------------------
  // Walks a component subtree and produces nested layer descriptors. The
  // component root is the origin for all rects.
  function buildLayer(el, root, parentEl, depth) {
    var cs = getComputedStyle(el);
    var dataLayer = el.getAttribute("data-layer");
    if (dataLayer === "skip") return null; // explicitly excluded

    var kind = classify(el, cs);
    var dataName = el.getAttribute("data-name");
    var dataFormat = el.getAttribute("data-format");
    var dataAnchor = el.getAttribute("data-anchor");
    var dataSlice = el.getAttribute("data-9slice");

    var rect = relRect(el, root);
    var isText = kind === "text";
    var anchor = inferAnchor(el, parentEl, dataAnchor, isText, cs);

    var node = {
      name: uniqueName(dataName || kind + (el.id ? "_" + el.id : "")),
      kind: kind,
      selector: cssPath(el),
      rect: rect,
      anchor: anchor,
    };

    if (isText) {
      // Text is never rasterized: record it as a live TMP node.
      node.text = textOf(el);
      node.font = (cs.fontFamily || "").split(",")[0].replace(/['"]/g, "").trim();
      node.size = r2(px(cs.fontSize) * Math.max(sx, sy));
      node.weight = cs.fontWeight;
      node.color = toHex(cs.color);
      node.align =
        cs.textAlign === "start" ? "left" :
        cs.textAlign === "end" ? "right" : (cs.textAlign || "left");
      return node; // text layers are leaves
    }

    if (kind === "icon") {
      node.format = inferFormat(el, kind, dataFormat);
      // icons never get a 9-slice
      node.nineSlice = null;
      return node;
    }

    if (kind === "sprite") {
      node.format = inferFormat(el, kind, dataFormat); // usually png
      // 9-slice: honor data-9slice (none | "L T R B" | auto), else auto.
      if (dataSlice === "none") {
        node.nineSlice = null;
      } else if (dataSlice && dataSlice !== "auto") {
        var nums = dataSlice.trim().split(/\\s+/).map(Number);
        node.nineSlice = (nums.length === 4 && nums.every(function (n) { return !isNaN(n); })) ? nums : autoNineSlice(el, cs, rect);
      } else {
        node.nineSlice = autoNineSlice(el, cs, rect);
      }
    } else {
      node.nineSlice = null; // group
    }

    // Recurse into element children to peel off text/icons/nested frames.
    var children = [];
    var kids = elChildren(el);
    for (var i = 0; i < kids.length; i++) {
      var child = buildLayer(kids[i], root, el, depth + 1);
      if (child) children.push(child);
    }
    if (children.length) node.children = children;
    return node;
  }

  // ---- component-root detection ----------------------------------------
  // Each element with data-ui is a component. If none are tagged, treat every
  // direct STYLED child block of <body> as its own component (so a multi-widget
  // mockup exports several components).
  function findComponentRoots() {
    var tagged = Array.prototype.slice.call(document.querySelectorAll("[data-ui]"))
      .filter(function (el) { return el.getAttribute("data-ui") !== "screen"; });
    // "screen" is a whole-screen marker — we are component-only, so we look
    // INSIDE a screen for its child components instead of exporting the screen.
    if (tagged.length) return tagged;

    var roots = [];
    var body = document.body;
    var kids = elChildren(body);
    for (var i = 0; i < kids.length; i++) {
      var el = kids[i];
      var cs = getComputedStyle(el);
      // A styled block (visible frame/container) or one that holds content.
      if (hasBackground(cs) || hasBorder(cs) || hasShadow(cs) || el.children.length > 0 || hasOwnText(el)) {
        roots.push(el);
      }
    }
    // Fallback: if body has only inline/styleless wrappers, use body itself.
    return roots.length ? roots : [body];
  }

  var out = [];
  var roots = findComponentRoots();
  for (var i = 0; i < roots.length; i++) {
    nameCounts = {}; // names are unique per component
    var rootEl = roots[i];
    var dataUi = rootEl.getAttribute("data-ui");
    var compName = uniqueName(rootEl.getAttribute("data-name") || dataUi || "component");
    var tree = buildLayer(rootEl, rootEl, rootEl.parentElement, 0);
    // The component wrapper itself is the first node; expose its archetype.
    out.push({
      name: compName,
      archetype: dataUi || "component",
      rect: absRect(rootEl),       // absolute design rect (informational)
      node: tree,                  // the layer tree rooted at the component
    });
  }
  return { design: { width: dw, height: dh }, components: out };
})
`;

/**
 * Run the in-browser decomposition on an already-loaded page.
 *
 * @param {import("playwright").Page} page - a page with the HTML file loaded.
 * @param {number} [designWidth] - normalize geometry to this design width (px).
 * @param {number} [designHeight] - normalize geometry to this design height (px).
 * @returns {Promise<{design:{width:number,height:number}, components:Array}>}
 *   the decomposition: design size + an array of components, each with a layer tree.
 */
export async function decomposePage(page, designWidth, designHeight) {
  // `evaluate` of a function expression string lets us pass args into the page.
  return page.evaluate(
    ([src, w, h]) => {
      // eslint-disable-next-line no-eval
      const fn = eval(src);
      return fn(w, h);
    },
    [BROWSER_DECOMPOSE_SRC, designWidth ?? null, designHeight ?? null]
  );
}
