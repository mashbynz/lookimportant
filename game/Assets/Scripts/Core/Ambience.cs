using UnityEngine;
using UnityEngine.Rendering;

namespace LookImportant
{
    /// <summary>
    /// Indoor ambient light and reflections without a lighting bake: sets the
    /// ambient probe directly and renders one realtime reflection probe at
    /// start-up (and again when the desk lamp changes), so chrome and brass
    /// reflect the room instead of black.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        public Color sky = new(0.20f, 0.22f, 0.24f);
        public Color ground = new(0.07f, 0.06f, 0.05f);
        public ReflectionProbe probe;

        static Ambience instance;
        float refreshAt = -1f;

        void Awake()
        {
            instance = this;
            var sh = new SphericalHarmonicsL2();
            Color avg = (sky + ground) * 0.5f;
            sh.AddAmbientLight(avg.linear);
            sh.AddDirectionalLight(Vector3.up, ((sky - ground) * 0.6f).linear, 1f);
            RenderSettings.ambientProbe = sh;
        }

        void Start() => refreshAt = Time.time + 0.1f;

        public static void RefreshReflectionsSoon()
        {
            if (instance != null) instance.refreshAt = Time.time + 0.4f;
        }

        void Update()
        {
            if (refreshAt < 0f || Time.time < refreshAt) return;
            refreshAt = -1f;
            if (probe != null) probe.RenderProbe();
        }
    }
}
