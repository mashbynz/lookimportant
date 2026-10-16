using UnityEditor;
using UnityEngine;

namespace LookImportant.EditorTools
{
    /// <summary>Prints imported transforms so Blender-to-Unity axis handling can be checked from batch mode.</summary>
    public static class Diagnostics
    {
        public static void DumpTransforms()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SceneBuilder.ModelPath);
            foreach (var n in new[] { "Gauge", "Gauge_NeedlePivot", "Gauge_Needle", "Toggle_1_Pivot", "ControlBox_Panel", "DialUnit_Panel",
                                      "DialUnit_KnobPivot", "KnifeSwitch_Pivot", "BigButton_Plunger", "Tool_WrenchLarge",
                                      "Padlock_Pivot", "Mug", "Pegboard", "Bench_Top", "Camera_Point" })
            {
                Transform t = null;
                foreach (var c in model.GetComponentsInChildren<Transform>(true)) if (c.name == n) { t = c; break; }
                if (t == null) { Debug.Log($"[Dump] {n} missing"); continue; }
                Debug.Log($"[Dump] {n}: localEuler={t.localEulerAngles} pos={t.position} right={t.right} up={t.up} fwd={t.forward} scale={t.lossyScale}");
            }
            EditorApplication.Exit(0);
        }

        public static void DumpAudio()
        {
            Debug.Log($"[Dump] active target {EditorUserBuildSettings.activeBuildTarget}");
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-reimport") >= 0)
                AssetDatabase.ImportAsset("Assets/Art/Audio", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Art/Audio" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                var importer = AssetImporter.GetAtPath(path);
                Debug.Log($"[Dump] {path}: type={type} importer={importer?.GetType().Name} clip={AssetDatabase.LoadAssetAtPath<AudioClip>(path) != null}");
            }
            EditorApplication.Exit(0);
        }
    }
}
