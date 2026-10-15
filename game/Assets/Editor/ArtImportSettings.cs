using UnityEditor;
using UnityEngine;

namespace LookImportant.EditorTools
{
    /// <summary>Import rules for everything under Assets/Art so settings never depend on clicking in the Inspector.</summary>
    class ArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Art/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;          // Blender Z-up -> Unity Y-up without -90 rotations
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = false;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.indexFormat = ModelImporterIndexFormat.Auto;
            mi.addCollider = false;
            mi.generateSecondaryUV = false;
            mi.sortHierarchyByName = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.png");
            bool dial = assetPath.Contains("/gauge_dial/") || assetPath.Contains("/dial_scale/");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.anisoLevel = dial ? 8 : 4;
            ti.wrapMode = dial ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Art/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings;
            bool loop = assetPath.EndsWith("_loop.wav");
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
        }
    }
}
