using UnityEngine;

namespace LookImportant
{
    /// <summary>Per-renderer emission without instantiating materials.</summary>
    public static class Emission
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static MaterialPropertyBlock block;

        /// <summary>Colour is linear HDR; SetColor expects gamma-encoded values and linearises them.</summary>
        public static void Set(Renderer r, Color linearHdr)
        {
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColor, linearHdr.gamma);
            r.SetPropertyBlock(block);
        }
    }
}
