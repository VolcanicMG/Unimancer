/**
 * Lazy Playwright loader for the HTML->Unity bridge tools.
 *
 * WHY lazy: the repo rule forbids adding a dependency (npm install) without the
 * user's approval, and Playwright is NOT yet a pinned dependency. We still ship
 * the full bridge tool code so it is ready the moment Playwright is approved;
 * until then, importing it here throws a single, clear, actionable error instead
 * of crashing the whole MCP server at startup with an unresolved import.
 *
 * We prefer `playwright` (bundled browsers) but accept `playwright-core` too,
 * since either satisfies the chromium API surface we use.
 */

/** Exact pinned-version install command surfaced to the user when missing. */
export const PLAYWRIGHT_INSTALL_HINT =
  "Playwright is not installed. It must be approved and added as a PINNED dependency before the HTML bridge tools can run. " +
  "Run (with approval): `guard install playwright@1.56.0` then `npx playwright install chromium`, " +
  "and verify it is pinned exactly (no ^/~) in package.json.";

/**
 * Dynamically import Chromium from whichever Playwright package is installed.
 * Throws a descriptive Error (with the install hint) when neither is present, so
 * the caller can surface it as a handled tool error.
 *
 * @returns {Promise<import("playwright").BrowserType>} the chromium browser type.
 * @throws {Error} when no Playwright package is installed.
 */
export async function loadChromium() {
  // Try the full package first, then the headless-core variant.
  for (const pkg of ["playwright", "playwright-core"]) {
    try {
      const mod = await import(pkg);
      const chromium = mod.chromium ?? mod.default?.chromium;
      if (chromium) return chromium;
    } catch {
      // Not installed / not resolvable — try the next candidate.
    }
  }
  throw new Error(PLAYWRIGHT_INSTALL_HINT);
}

/**
 * Launch a headless Chromium browser, run `fn` with a fresh page, and always
 * close the browser afterward. Centralizes lifecycle so each tool does not
 * re-implement launch/teardown.
 *
 * @template T
 * @param {(page: import("playwright").Page, browser: import("playwright").Browser) => Promise<T>} fn
 *   callback receiving an open page (and the browser, for screenshot scale tweaks).
 * @param {{ deviceScaleFactor?: number, viewport?: {width:number,height:number} }} [opts]
 *   optional context options (e.g. exportScale -> deviceScaleFactor for crisp PNGs).
 * @returns {Promise<T>} whatever `fn` resolves to.
 */
export async function withPage(fn, opts = {}) {
  const chromium = await loadChromium();
  const browser = await chromium.launch({ headless: true });
  try {
    const context = await browser.newContext({
      deviceScaleFactor: opts.deviceScaleFactor ?? 1,
      viewport: opts.viewport ?? { width: 1280, height: 720 },
    });
    const page = await context.newPage();
    return await fn(page, browser);
  } finally {
    await browser.close();
  }
}
