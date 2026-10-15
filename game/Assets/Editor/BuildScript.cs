using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LookImportant.EditorTools
{
    /// <summary>
    /// Batch builds. From the repo root, tools/build.sh wraps this:
    ///   Unity -batchmode -projectPath game -executeMethod LookImportant.EditorTools.BuildScript.Run
    ///         -lookimportant-targets linux,windows,webgl
    /// Players land in Builds/&lt;Platform&gt;/ (git-ignored). The version comes from
    /// the newest changelog.md entry.
    /// </summary>
    public static class BuildScript
    {
        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static void Run()
        {
            int failures = 0;
            try
            {
                if (!HasArg("-lookimportant-skip-scene"))
                    SceneBuilder.Build();
                ConfigurePlayer();
                string targets = Arg("-lookimportant-targets") ?? "linux";
                foreach (var t in targets.Split(','))
                    if (!Build(t.Trim())) failures++;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        /// <summary>Rebuild the scene only (no players).</summary>
        public static void RunSceneOnly()
        {
            int code = 0;
            try { SceneBuilder.Build(); ConfigurePlayer(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Look Important/Build Linux")]
        static void MenuLinux() { ConfigurePlayer(); Build("linux"); }
        [MenuItem("Look Important/Build Windows")]
        static void MenuWindows() { ConfigurePlayer(); Build("windows"); }
        [MenuItem("Look Important/Build WebGL")]
        static void MenuWebGL() { ConfigurePlayer(); Build("webgl"); }

        static bool Build(string target)
        {
            (BuildTarget bt, string rel) = target switch
            {
                "linux" => (BuildTarget.StandaloneLinux64, "Builds/Linux/LookImportant.x86_64"),
                "windows" => (BuildTarget.StandaloneWindows64, "Builds/Windows/LookImportant.exe"),
                "webgl" => (BuildTarget.WebGL, "Builds/WebGL"),
                _ => throw new ArgumentException($"unknown target '{target}' (linux, windows, webgl)")
            };
            string path = Path.Combine(RepoRoot, rel);
            string dir = target == "webgl" ? path : Path.GetDirectoryName(path);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = path,
                target = bt,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[BuildScript] {target}: {s.result}, {s.totalSize / 1e6:F1} MB, {s.totalErrors} errors, " +
                      $"{s.totalWarnings} warnings, {s.totalTime.TotalSeconds:F0}s -> {path}");
            return s.result == BuildResult.Succeeded;
        }

        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Matt Ashby";
            PlayerSettings.productName = "Look Important";
            PlayerSettings.bundleVersion = Version();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "nz.ashbys.lookimportant");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "nz.ashbys.lookimportant");

            // No splash, no launcher dialog, no settings: the game just opens on the bench.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.usePlayerLog = true;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // works from any static file server
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            AssetDatabase.SaveAssets();
        }

        static string Version()
        {
            var changelog = Path.Combine(RepoRoot, "changelog.md");
            if (!File.Exists(changelog)) return "0.0.0";
            var m = Regex.Match(File.ReadAllText(changelog), @"^##\s+([0-9][0-9.]*)", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : "0.0.0";
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
    }
}
