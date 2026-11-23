using UnityEngine;

public abstract class ShopPanelBase : MonoBehaviour
{
    public static bool IsAnyShopOpen { get; protected set; }

    public bool IsOpen { get; protected set; }

    public abstract void Open();
    public abstract void Close();
}