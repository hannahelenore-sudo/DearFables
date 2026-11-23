using UnityEngine;

public static class BattleEnemy
{
    public static Sprite Sprite { get; private set; }

    public static bool HasEnemy { get { return Sprite != null; } }

    public static bool HasStyle { get; private set; }
    public static bool Flip { get; private set; }
    public static float Scale { get; private set; }
    public static Vector3 Offset { get; private set; }
    public static Vector2 ImageOffset { get; private set; }

    public static void Set(Sprite sprite)
    {
        Sprite = sprite;
        HasStyle = false;
    }

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