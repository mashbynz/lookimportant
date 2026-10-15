using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Alpha wiring between the props - a placeholder for the real puzzle logic.
    ///   knife switch = mains. Nothing on the bench is live without it.
    ///   toggle n     -> pilot lamp n (when live)
    ///   selector     -> gauge base reading; each lit lamp adds load
    ///   red button   -> overload spike into the red, with a buzz
    /// </summary>
    public class WorkbenchCircuit : MonoBehaviour
    {
        public KnifeSwitch mains;
        public ToggleSwitch[] toggles;
        public IndicatorLamp[] lamps;
        public RotaryKnob selector;
        public Gauge gauge;
        public PushButton button;
        public AudioSource hum;
        public AudioSource buzz;
        public float humVolume = 0.22f;
        public float buzzVolume = 0.3f;

        void Update()
        {
            bool live = mains.IsLive;
            int lit = 0;
            for (int i = 0; i < lamps.Length; i++)
            {
                bool on = live && toggles[i].IsOn;
                lamps[i].SetLit(on);
                if (on) lit++;
            }

            float reading = 0f;
            if (live)
            {
                reading = 0.12f + 0.14f * selector.Index + 0.05f * lit;
                if (button.IsHeld) reading += 0.35f;
            }
            gauge.target = Mathf.Clamp01(reading);
            gauge.live = live;

            Fade(hum, live ? humVolume : 0f, 1.5f);
            Fade(buzz, live && button.IsHeld ? buzzVolume : 0f, 6f);
        }

        static void Fade(AudioSource s, float target, float rate)
        {
            if (s == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target, Time.deltaTime * rate);
            if (s.volume > 0f && !s.isPlaying) s.Play();
            else if (s.volume <= 0f && s.isPlaying) s.Stop();
        }
    }
}
