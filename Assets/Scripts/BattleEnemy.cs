using UnityEngine;

/// <summary>
/// Hands the enemy's picture from the overworld to the battle scene.
/// EnemyChase calls Set() when it starts a battle; BattleManager reads it when the battle scene loads.
/// An enemy can also ask for its picture to be flipped / enlarged / moved in the battle (SetStyle).
/// Anything else that starts a battle can do the same before loading the scene.
/// </summary>
public static class BattleEnemy
{
    /// <summary>The sprite of the enemy that started the battle (null = none, e.g. a fight started from a zone).</summary>
    public static Sprite Sprite { get; private set; }

    public static bool HasEnemy { get { return Sprite != null; } }

    // Optional look for the picture in the battle. Only set by enemies that ask for it.
    public static bool HasStyle { get; private set; }
    public static bool Flip { get; private set; }
    public static float Scale { get; private set; }
    public static Vector3 Offset { get; private set; }
    public static Vector2 ImageOffset { get; private set; }

    /// <summary>Sets the picture and clears any custom look from an earlier fight.</summary>
    public static void Set(Sprite sprite)
    {
        Sprite = sprite;
        HasStyle = false;
    }

    /// <summary>Call after Set(): flip / scale / move the picture in the battle scene (only for this fight).</summary>
    public static void SetStyle(bool flip, float scale, Vector3 offset, Vector2 imageOffset)
    {
        HasStyle = true;
        Flip = flip;
        Scale = scale;
        Offset = offset;
        ImageOffset = imageOffset;
    }

    public static void Clear()
    {
        Sprite = null;
        HasStyle = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        Clear();
    }
}