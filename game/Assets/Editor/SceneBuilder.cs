using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LookImportant.EditorTools
{
    /// <summary>
    /// Generates Assets/Scenes/Workbench.unity from the Blender export. Blender
    /// owns geometry and layout (object names, pivots, *_Point/*_Target empties);
    /// this owns behaviour, lights, audio, colliders and post-processing.
    /// Re-run after re-exporting from Blender. Hand edits to the scene are lost.
    ///
    /// Axis notes (checked with Diagnostics.DumpTransforms): world Blender
    ///   (x, y, z) lands at Unity (x, z, y), and every object's local frame is
    ///   Blender's with Z negated: Unity local X = Blender X, Y = Blender Y,
    ///   Z = Blender -Z. A Blender rotation of +a about local X or Y is -a about
    ///   the same Unity local axis; about Blender Z it is +a about Unity local -Z.
    ///   Unity rotations look clockwise when viewed from the axis tip, which is
    ///   how the 'clockwise to the player' axes below were chosen.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ModelPath = "Assets/Art/Models/WorkbenchScene.fbx";
        public const string ScenePath = "Assets/Scenes/Workbench.unity";
        const string VolumePath = "Assets/Settings/WorkbenchVolume.asset";
        const string LightingPath = "Assets/Settings/WorkbenchLighting.lighting";

        static Dictionary<string, Transform> parts;
        static HashSet<Transform> moving;
        static Dictionary<string, MaterialLibrary.Entry> palette;

        [MenuItem("Look Important/Rebuild Workbench Scene")]
        public static void Build()
        {
            palette = MaterialLibrary.Load();
            MaterialLibrary.Sync();
            RemapModelMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "WorkbenchScene";
            parts = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                parts[t.name] = t;
            moving = new HashSet<Transform>();

            var circuit = new GameObject("Circuit").AddComponent<WorkbenchCircuit>();
            BuildControlBox(circuit);
            BuildButtonAndSelector(circuit);
            BuildKnifeSwitchAndGauge(circuit);
            BuildDeskLamp();
            BuildFidgets();
            BuildBlockers();
            BuildLighting();
            BuildAudio();
            BuildCamera();
            ApplyStaticAndShadows(root);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneBuilder] Built {ScenePath}");
        }

        static void RemapModelMaterials()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
            importer.SaveAndReimport();
        }

        // --- props -------------------------------------------------------------

        static void BuildControlBox(WorkbenchCircuit circuit)
        {
            string[] lensMats = { "M_LampAmber", "M_LampGreen", "M_LampRed" };
            circuit.toggles = new ToggleSwitch[3];
            circuit.lamps = new IndicatorLamp[3];
            for (int i = 0; i < 3; i++)
            {
                int n = i + 1;
                var pivot = P($"Toggle_{n}_Pivot");
                Collider(P($"Toggle_{n}_Lever"), 0.006f, 0.026f);
                var ts = pivot.gameObject.AddComponent<ToggleSwitch>();
                ts.axis = Vector3.right;
                ts.onAngle = 24f;       // tipped up the slope, away from the player
                ts.offAngle = -24f;
                ts.soundOn = Clip("toggle_on");
                ts.soundOff = Clip("toggle_off");
                moving.Add(pivot);
                circuit.toggles[i] = ts;

                var lampRoot = P($"Lamp_{n}");
                var lamp = lampRoot.gameObject.AddComponent<IndicatorLamp>();
                lamp.lens = P($"Lamp_{n}_Lens").GetComponent<Renderer>();
                lamp.litEmission = palette[lensMats[i]].LitEmission;
                circuit.lamps[i] = lamp;
            }
        }

        static void BuildButtonAndSelector(WorkbenchCircuit circuit)
        {
            var plunger = P("BigButton_Plunger");
            Collider(P("BigButton_Cap"), 0.003f);
            var button = plunger.gameObject.AddComponent<PushButton>();
            button.pressDirection = Vector3.forward;   // local +Z is down into the housing
            button.travel = 0.009f;
            button.soundDown = Clip("button_down");
            button.soundUp = Clip("button_up");
            moving.Add(plunger);
            circuit.button = button;

            var knobPivot = P("DialUnit_KnobPivot");
            Collider(P("DialUnit_Knob"), 0.004f);
            var knob = knobPivot.gameObject.AddComponent<RotaryKnob>();
            knob.axis = Vector3.back;   // panel normal (towards the player): positive = clockwise
            knob.detents = new[] { -60f, -30f, 0f, 30f, 60f };
            knob.index = 0;
            knob.soundDetent = Clip("knob_detent");
            moving.Add(knobPivot);
            circuit.selector = knob;
        }

        static void BuildKnifeSwitchAndGauge(WorkbenchCircuit circuit)
        {
            var hinge = P("KnifeSwitch_Pivot");
            Collider(P("KnifeSwitch_Handle"), 0.006f);
            Collider(P("KnifeSwitch_Blades"), 0.004f);
            var knife = hinge.gameObject.AddComponent<KnifeSwitch>();
            knife.axis = Vector3.left;    // positive swings the blades out towards the player
            knife.openAngle = 105f;
            knife.closed = false;
            knife.soundClose = Clip("knife_close");
            knife.soundOpen = Clip("knife_open");
            moving.Add(hinge);
            circuit.mains = knife;

            var gaugeRoot = P("Gauge");
            Collider(P("Gauge_Body"), 0.002f);
            var gauge = gaugeRoot.gameObject.AddComponent<Gauge>();
            gauge.needle = P("Gauge_NeedlePivot");
            gauge.axis = Vector3.down;     // dial normal (towards the player): positive = clockwise
            gauge.minAngle = -135f;
            gauge.maxAngle = 135f;
            gauge.soundTap = Clip("glass_tap");
            moving.Add(gauge.needle);
            circuit.gauge = gauge;
        }

        static void BuildDeskLamp()
        {
            var root = P("DeskLamp");
            MeshCollider(P("DeskLamp_Base"));
            Collider(P("DeskLamp_Shade"), 0.004f);
            Collider(P("DeskLamp_Switch"), 0.006f);
            var light = SpotAt("DeskLamp_Point", "DeskLamp_Target", "DeskLampLight");
            light.color = new Color(1f, 0.83f, 0.62f);
            light.intensity = 0.4f;
            light.range = 3f;
            light.spotAngle = 90f;
            light.innerSpotAngle = 40f;
            light.shadowNearPlane = 0.03f;
            light.shadows = LightShadows.Hard;      // soft shadows cost ~1.3 ms on an HD 6000
            var lamp = root.gameObject.AddComponent<DeskLamp>();
            lamp.lamp = light;
            lamp.bulb = P("DeskLamp_Bulb").GetComponent<Renderer>();
            lamp.bulbEmission = palette["M_Bulb"].LitEmission;
            lamp.on = true;
            lamp.soundSwitch = Clip("lamp_click");
        }

        static void BuildFidgets()
        {
            Swing("Tool_WrenchLarge", "Tool_WrenchLarge_Mesh", 1.35f, 0.035f, 150f, 22f, "clink_large");
            Swing("Tool_WrenchSmall", "Tool_WrenchSmall_Mesh", 1.6f, 0.035f, 170f, 22f, "clink_small");
            Swing("Tool_Hammer", "Tool_Hammer_Mesh", 3.2f, 0.12f, 70f, 6f, "clink_hammer");
            Swing("Tool_WireCoil", "Tool_WireCoil_Mesh", 2.2f, 0.06f, 140f, 20f, "rattle_plastic");
            for (int i = 1; i <= 3; i++)
            {
                var w = Swing($"Tool_Screwdriver_{i}", $"Tool_Screwdriver_{i}_Mesh", 4.5f, 0.12f, 60f, 7f, "rattle_plastic");
                w.primaryAxis = Vector3.up;      // tip sideways in the rack
                w.secondaryAxis = Vector3.right; // and towards/away from the board
            }
            var padlock = Swing("Padlock_Pivot", "Padlock_Mesh", 4.5f, 0.12f, 220f, 32f, "padlock_rattle");
            padlock.primaryAxis = Vector3.right;   // swing out from the hasp
            padlock.secondaryAxis = Vector3.up;     // and side to side
            var mug = Swing("Mug", "Mug_Mesh", 6f, 0.25f, 45f, 3.5f, "mug_tink");
            mug.primaryAxis = Vector3.right;
            mug.secondaryAxis = Vector3.up;
            var loose = Swing("Loose_Screwdriver", "Loose_Screwdriver_Mesh", 1.8f, 0.3f, 260f, 40f, "rattle_plastic");
            loose.primaryAxis = Vector3.forward;   // rolls about its own shaft
        }

        static Wobble Swing(string pivotName, string meshName, float freq, float damping, float kick, float max, string clip)
        {
            var pivot = P(pivotName);
            Collider(P(meshName), 0.004f);
            var w = pivot.gameObject.AddComponent<Wobble>();
            w.primaryAxis = Vector3.up;   // pegboard normal: swing side to side against the board
            w.frequency = freq;
            w.damping = damping;
            w.kick = kick;
            w.maxAngle = max;
            w.sound = Clip(clip);
            moving.Add(pivot);
            return w;
        }

        static void BuildBlockers()
        {
            // Static props stop clicks reaching things hidden behind them.
            foreach (var n in new[] { "Bench_Top", "Pegboard_Board", "ControlBox_Housing", "ControlBox_Faceplate",
                                      "DialUnit_Housing", "DialUnit_Plate", "BigButton_Housing", "KnifeSwitch_Base",
                                      "LockBox_Body", "ScrewdriverRack_Mesh" })
                Collider(P(n), 0f);
        }

        // --- environment -------------------------------------------------------

        static void BuildLighting()
        {
            var ceiling = SpotAt("CeilingLight_Point", "CeilingLight_Target", "CeilingLight");
            ceiling.color = new Color(0.86f, 0.93f, 1f);
            ceiling.intensity = 3.0f;
            ceiling.range = 6f;
            ceiling.spotAngle = 125f;
            ceiling.innerSpotAngle = 60f;
            ceiling.shadows = LightShadows.None;   // fill light: its shadow pass cost 1.6 ms on an HD 6000

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.32f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.20f, 0.20f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.09f, 0.08f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = false;

            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (lighting == null)
            {
                lighting = new LightingSettings();
                AssetDatabase.CreateAsset(lighting, LightingPath);
            }
            lighting.bakedGI = false;
            lighting.realtimeGI = false;
            Lightmapping.lightingSettings = lighting;

            // Box-projected reflections of the room for chrome, brass and copper.
            var bounds = new Bounds(P("Room_Floor").GetComponent<Renderer>().bounds.center, Vector3.zero);
            foreach (var n in new[] { "Room_Floor", "Room_WallBack", "Room_WallSides", "Room_Ceiling" })
                bounds.Encapsulate(P(n).GetComponent<Renderer>().bounds);
            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0f, 1.3f, 0.2f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.center = bounds.center - probe.transform.position;
            probe.size = bounds.size;
            probe.nearClipPlane = 0.05f;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Color.black;

            var ambience = new GameObject("Ambience").AddComponent<Ambience>();
            ambience.probe = probe;
            ambience.sky = new Color(0.40f, 0.42f, 0.44f);
            ambience.ground = new Color(0.12f, 0.11f, 0.10f);
        }

        static void BuildAudio()
        {
            var room = new GameObject("RoomTone").AddComponent<AudioSource>();
            Loop(room, "roomtone_loop", 0.35f, 0f, true);
            var fluoro = P("CeilingLight").gameObject.AddComponent<AudioSource>();
            Loop(fluoro, "fluoro_loop", 0.05f, 1f, true);

            var circuit = Object.FindAnyObjectByType<WorkbenchCircuit>();
            circuit.hum = P("KnifeSwitch").gameObject.AddComponent<AudioSource>();
            Loop(circuit.hum, "hum_loop", 0f, 0.8f, false);
            circuit.buzz = P("Gauge").gameObject.AddComponent<AudioSource>();
            Loop(circuit.buzz, "buzz_loop", 0f, 0.8f, false);
        }

        static void Loop(AudioSource s, string clip, float volume, float spatial, bool play)
        {
            s.clip = Clip(clip);
            s.loop = true;
            s.volume = volume;
            s.spatialBlend = spatial;
            s.playOnAwake = play;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.5f;
            s.maxDistance = 8f;
            s.dopplerLevel = 0f;
        }

        static void BuildCamera()
        {
            var go = new GameObject("PlayerCamera") { tag = "MainCamera" };
            var from = P("Camera_Point").position;
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(P("Camera_Target").position - from, Vector3.up));
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 12f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;   // MSAA x4 cost 6 ms on an HD 6000
            go.AddComponent<AudioListener>();
            go.AddComponent<DeskCamera>();
            go.AddComponent<InteractionController>();
            go.AddComponent<AdaptiveResolution>();
            go.AddComponent<AutoCapture>();

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = BuildVolumeProfile();
            go.AddComponent<StartupFade>().volume = volume;
        }

        static VolumeProfile BuildVolumeProfile()
        {
            AssetDatabase.DeleteAsset(VolumePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
            var tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.6f);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(4);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.3f);
            color.contrast.Override(10f);
            color.saturation.Override(6f);
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static void ApplyStaticAndShadows(GameObject root)
        {
            var noShadows = new HashSet<string> { "Room_Floor", "Room_WallBack", "Room_WallSides", "Room_Ceiling",
                "Room_Skirting", "Pegboard_Board", "Pegboard_Backing", "CeilingLight_Tubes", "CeilingLight_Housing",
                "DeskLamp_Bulb" };
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (noShadows.Contains(r.name)) r.shadowCastingMode = ShadowCastingMode.Off;
                if (!IsMoving(r.transform))
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        // --- helpers -----------------------------------------------------------

        static Transform P(string name)
        {
            if (!parts.TryGetValue(name, out var t))
                throw new System.Exception($"[SceneBuilder] '{name}' not found in {ModelPath} - was it renamed in blender/build_scene.py?");
            return t;
        }

        static bool IsMoving(Transform t)
        {
            for (; t != null; t = t.parent)
                if (moving.Contains(t)) return true;
            return false;
        }

        static AudioClip Clip(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Art/Audio/{name}.wav");
            if (c == null) throw new System.Exception($"[SceneBuilder] missing clip {name}");
            return c;
        }

        /// <summary>Box collider fitted to the mesh, padded, and at least minSize on each axis.</summary>
        static void Collider(Transform t, float pad, float minSize = 0f)
        {
            var mf = t.GetComponent<MeshFilter>();
            var b = mf.sharedMesh.bounds;
            var bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = b.center;
            var size = b.size + Vector3.one * (2f * pad);
            bc.size = Vector3.Max(size, Vector3.one * minSize);
        }

        static void MeshCollider(Transform t)
        {
            t.gameObject.AddComponent<MeshCollider>().sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
        }

        static Light SpotAt(string point, string target, string name)
        {
            var p = P(point);
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.rotation = Quaternion.LookRotation(P(target).position - p.position, Vector3.up);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.GetUniversalAdditionalLightData();   // default shadow tier is High
            return light;
        }
    }
}
