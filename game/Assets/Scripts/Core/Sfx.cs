using UnityEngine;

namespace LookImportant
{
    /// <summary>Pooled one-shot sounds, positioned in 3D so clicks pan across the bench.</summary>
    public static class Sfx
    {
        const int PoolSize = 12;
        static AudioSource[] pool;
        static int next;

        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            EnsurePool();
            var s = pool[next];
            next = (next + 1) % PoolSize;
            s.transform.position = position;
            s.pitch = pitch * Random.Range(0.96f, 1.04f);
            s.volume = volume;
            s.clip = clip;
            s.Play();
        }

        static void EnsurePool()
        {
            if (pool != null && pool[0] != null) return;
            var root = new GameObject("Sfx");
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(root.transform);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0.65f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 0.4f;
                s.maxDistance = 8f;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }
        }
    }
}
