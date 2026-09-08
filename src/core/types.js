/**
 * Core contract shared by every Unimancer tool, expressed as JSDoc typedefs.
 *
 * A tool is self-describing: a name, an LLM-facing description, a Zod input
 * schema (raw shape, as the MCP SDK expects), and an async handler. Tools shell
 * out to `adb`/`emulator`/Playwright directly; the one step that needs the Unity
 * Editor goes through `unityCommand()` (core/unityCli.js), not through `ctx`.
 */

/**
 * @typedef {Object} ToolResultContent
 * @property {"text"|"image"} type
 * @property {string} [text] - present when type==="text".
 * @property {string} [data] - base64-encoded image bytes when type==="image".
 * @property {string} [mimeType] - image MIME type when type==="image".
 */

/**
 * @typedef {Object} ToolResult
 * @property {ToolResultContent[]} content
 * @property {boolean} [isError] - true marks a handled failure.
 */

/**
 * @typedef {Object} ToolContext - shared per-server state passed to every handler (currently empty).
 */

/**
 * @typedef {Object} ToolDefinition
 * @property {string} name - unique MCP tool name (snake_case, e.g. `adb_install`).
 * @property {string} description - what the tool does, for an LLM choosing to call it.
 * @property {Record<string, import("zod").ZodTypeAny>} inputSchema - Zod raw shape for params.
 * @property {(args: any, ctx: ToolContext) => Promise<ToolResult>} handler - executes the tool.
 */

/**
 * Build a successful text result.
 * @param {string} text
 * @returns {ToolResult}
 */
export function ok(text) {
  return { content: [{ type: "text", text }] };
}

/**
 * Build a handled error result (not thrown).
 * @param {string} text
 * @returns {ToolResult}
 */
export function err(text) {
  return { content: [{ type: "text", text }], isError: true };
}

/**
 * Build a successful result carrying an inline image (plus an optional caption).
 * Tools that render a visual preview return this so a client (e.g. the in-editor
 * chat) can display the picture directly instead of only text.
 * @param {string} base64 - base64-encoded image bytes (no `data:` prefix).
 * @param {string} [mimeType] - image MIME type (default "image/png").
 * @param {string} [caption] - optional leading text block (e.g. a summary).
 * @returns {ToolResult}
 */
export function okImage(base64, mimeType = "image/png", caption) {
  const content = [];
  if (caption) content.push({ type: "text", text: caption });
  content.push({ type: "image", data: base64, mimeType });
  return { content };
}

/**
 * Build a successful result carrying MULTIPLE inline images (plus an optional
 * caption). Used by tools that return several previews at once — e.g. one cropped
 * image per detected component — so the client renders each picture SEPARATELY
 * instead of one composite. Each image becomes its own content block.
 * @param {Array<{base64:string, mimeType?:string}>} images - images in display order.
 * @param {string} [caption] - optional leading text block (e.g. a summary).
 * @returns {ToolResult}
 */
export function okImages(images, caption) {
  const content = [];
  if (caption) content.push({ type: "text", text: caption });
  for (const im of images) {
    content.push({ type: "image", data: im.base64, mimeType: im.mimeType ?? "image/png" });
  }
  return { content };
}
