using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LookImportant.EditorTools
{
    /// <summary>
    /// Creates/updates URP Lit materials in Assets/Art/Materials from
    /// Assets/Art/materials.json, the palette Blender also renders with.
    /// Imported FBX materials are remapped to these by name.
    /// </summary>
    public static class MaterialLibrary
    {
        public const string JsonPath = "Assets/Art/materials.json";
        public const string Folder = "Assets/Art/Materials";

        public class Entry
        {
            public string name;
            public Color color = Color.white;
            public float metallic, smoothness = 0.5f;
            public string texture;
            public float tile = 1f, normalScale = 1f;
            public bool hasEmission, lit;
            public Color emission = Color.black;
            public float emissionOn = 1f;

            /// <summary>Linear HDR emission when switched on.</summary>
            public Color LitEmission => emission.linear * emissionOn;
        }

        public static Dictionary<string, Entry> Load()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(JsonPath));
            var result = new Dictionary<string, Entry>();
            foreach (var kv in root)
            {
                if (kv.Key.StartsWith("_")) continue;
                var d = (Dictionary<string, object>)kv.Value;
                var e = new Entry { name = kv.Key, color = ColorOf(d["color"]) };
                if (d.TryGetValue("metallic", out var m)) e.metallic = (float)(double)m;
                if (d.TryGetValue("smoothness", out var s)) e.smoothness = (float)(double)s;
                if (d.TryGetValue("texture", out var t)) e.texture = (string)t;
                if (d.TryGetValue("tile_m", out var tm)) e.tile = (float)(double)tm;
                if (d.TryGetValue("normal_scale", out var ns)) e.normalScale = (float)(double)ns;
                if (d.TryGetValue("emission", out var em)) { e.hasEmission = true; e.emission = ColorOf(em); }
                if (d.TryGetValue("emission_on", out var eo)) e.emissionOn = (float)(double)eo;
                if (d.TryGetValue("lit", out var lit)) e.lit = (bool)lit;
                result[e.name] = e;
            }
            return result;
        }

        static Color ColorOf(object o)
        {
            var l = (List<object>)o;
            return new Color((float)(double)l[0], (float)(double)l[1], (float)(double)l[2]);
        }

        public static Dictionary<string, Material> Sync()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Art", "Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mats = new Dictionary<string, Material>();
            foreach (var e in Load().Values)
            {
                string path = $"{Folder}/{e.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = shader;
                m.SetFloat("_WorkflowMode", 1f);
                m.SetColor("_BaseColor", e.color);
                m.SetFloat("_Metallic", e.metallic);
                m.SetFloat("_Smoothness", e.smoothness);

                Texture2D albedo = null, normal = null;
                if (e.texture != null)
                {
                    albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Textures/{e.texture}/{e.texture}_albedo.png");
                    normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Textures/{e.texture}/{e.texture}_normal.png");
                }
                m.SetTexture("_BaseMap", albedo);
                m.SetTextureScale("_BaseMap", Vector2.one / e.tile);
                SetKeyword(m, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", albedo != null);
                m.SetFloat("_SmoothnessTextureChannel", albedo != null ? 1f : 0f);
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", e.normalScale);
                SetKeyword(m, "_NORMALMAP", normal != null);

                // Emission is enabled everywhere: hover glow and lamps drive it per renderer.
                SetKeyword(m, "_EMISSION", true);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                m.SetColor("_EmissionColor", e.lit ? e.LitEmission.gamma : Color.black);
                m.enableInstancing = false;
                EditorUtility.SetDirty(m);
                mats[e.name] = m;
            }
            AssetDatabase.SaveAssets();
            return mats;
        }

        static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }
    }
}
