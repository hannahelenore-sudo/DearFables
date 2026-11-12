using UnityEngine;

/// <summary>
/// Common base for every shop screen (TrinketShopUI, BlueprintShopUI) so ShopInteractable can open either one.
/// Existing scene references keep working: a TrinketShopUI is still a TrinketShopUI, it just also is a ShopPanelBase.
/// </summary>
public abstract class ShopPanelBase : MonoBehaviour
{
    /// <summary>True while any shop is open. Handy to stop player movement: if (TrinketShopUI.IsAnyShopOpen) return;</summary>
    public static bool IsAnyShopOpen { get; protected set; }

    public bool IsOpen { get; protected set; }

    public abstract void Open();
    public abstract void Close();
}
