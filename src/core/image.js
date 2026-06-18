/**
 * Helper for returning rendered images to the MCP client as image content
 * blocks (so the model can actually SEE the capture, not just a file path).
 * Kept separate from types.js to avoid coupling the text helpers to image IO.
 */

/**
 * Build a ToolResult carrying a base64 image, with an optional text caption.
 * @param {string} base64 - base64-encoded image bytes (no data: prefix).
 * @param {string} [mimeType] - image MIME type (default "image/png").
 * @param {string} [caption] - optional text appended after the image.
 * @returns {import("./types.js").ToolResult}
 */
export function image(base64, mimeType = "image/png", caption) {
  /** @type {any[]} */
  const content = [{ type: "image", data: base64, mimeType }];
  if (caption) content.push({ type: "text", text: caption });
  return { content };
}
