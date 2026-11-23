using UnityEngine;

public static class GameSFX
{
    private const int PoolSize = 12;

    private static AudioSource[] pool;
    private static int nextIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pool = null;
        nextIndex = 0;
    }

    private static void EnsurePool()
    {
        if (pool != null && pool[0] != null) return;

        GameObject host = new GameObject("GameSFX");
        Object.DontDestroyOnLoad(host);

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            pool[i] = src;
        }
    }

    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        EnsurePool();

        AudioSource chosen = null;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!pool[i].isPlaying)
            {
                chosen = pool[i];
                break;
            }
        }

        if (chosen == null)
        {
            chosen = pool[nextIndex];
            nextIndex = (nextIndex + 1) % pool.Length;
        }

        chosen.clip = clip;
        chosen.volume = Mathf.Clamp01(volume);
        chosen.pitch = pitch;
        chosen.Play();
    }
}