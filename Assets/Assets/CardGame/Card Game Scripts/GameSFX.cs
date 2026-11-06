using UnityEngine;

/// <summary>
/// Tiny shared sound-effect player. No setup needed: it creates a hidden persistent
/// GameObject with a small pool of 2D AudioSources the first time a sound is played.
/// Each sound gets its own source, so changing the pitch of one never affects another.
/// </summary>
public static class GameSFX
{
    private const int PoolSize = 12;

    private static AudioSource[] pool;
    private static int nextIndex;

    // Keeps things safe when "Enter Play Mode Options > Reload Domain" is turned off
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
            src.spatialBlend = 0f; // 2D, so it is always heard no matter which camera is active
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

        // Every source is busy: reuse the next one in line
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
