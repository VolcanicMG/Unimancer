using Unity.Pipeline.Commands;

namespace Unimancer.Commands
{
    /// <summary>
    /// A {x,y} vector argument. Declared as a structured input so the command
    /// schema advertises a nested object rather than collapsing to a string —
    /// this keeps the JSON shape identical to the pre-CLI zod schemas.
    /// </summary>
    public class Vec2 : IStructuredCommandInput
    {
        /// <summary>X component.</summary>
        [CliArg("x", "X component.", Required = true)]
        public float x;

        /// <summary>Y component.</summary>
        [CliArg("y", "Y component.", Required = true)]
        public float y;

        /// <summary>Convert to a UnityEngine.Vector2.</summary>
        public UnityEngine.Vector2 ToVector2() => new UnityEngine.Vector2(x, y);
    }

    /// <summary>A 9-slice sprite border in pixels.</summary>
    public class SpriteBorder : IStructuredCommandInput
    {
        /// <summary>Left border in pixels.</summary>
        [CliArg("left", "Left border in pixels.", Required = true)]
        public float left;

        /// <summary>Top border in pixels.</summary>
        [CliArg("top", "Top border in pixels.", Required = true)]
        public float top;

        /// <summary>Right border in pixels.</summary>
        [CliArg("right", "Right border in pixels.", Required = true)]
        public float right;

        /// <summary>Bottom border in pixels.</summary>
        [CliArg("bottom", "Bottom border in pixels.", Required = true)]
        public float bottom;
    }

    /// <summary>An {r,g,b,a} color with 0..255 channels; alpha defaults to 255.</summary>
    public class Rgba : IStructuredCommandInput
    {
        /// <summary>Red channel, 0..255.</summary>
        [CliArg("r", "Red channel, 0..255.", Required = true)]
        public float r;

        /// <summary>Green channel, 0..255.</summary>
        [CliArg("g", "Green channel, 0..255.", Required = true)]
        public float g;

        /// <summary>Blue channel, 0..255.</summary>
        [CliArg("b", "Blue channel, 0..255.", Required = true)]
        public float b;

        /// <summary>Alpha channel, 0..255. Omitted = the caller's fallback alpha (255 for color, 0 for color2).</summary>
        [CliArg("a", "Alpha channel, 0..255 (default: 255, or 0 for color2).")]
        public float a = -1f;

        /// <summary>Convert the 0..255 channels to a UnityEngine.Color.</summary>
        /// <param name="fallbackAlpha">0..1 alpha used when `a` was omitted.</param>
        public UnityEngine.Color ToColor(float fallbackAlpha = 1f) =>
            new UnityEngine.Color(r / 255f, g / 255f, b / 255f, a < 0f ? fallbackAlpha : a / 255f);
    }
}
