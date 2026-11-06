using UnityEngine;

/// <summary>
/// Put this on the trigger zone that starts the card game.
/// It asks the player's PlayerPositionSaver to save the position and load the card game scene.
/// The scene name is set on PlayerPositionSaver ("Battle Scene Name").
/// </summary>
public class ZoneLoadScene : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Player was just placed back inside this zone after a battle: ignore until they leave
        if (PlayerPositionSaver.BlockReentry) return;

        PlayerPositionSaver saver = other.GetComponentInParent<PlayerPositionSaver>();
        if (saver != null)
        {
            saver.EnterBattle();
        }
        else
        {
            Debug.LogWarning("Player has no PlayerPositionSaver component!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerPositionSaver.BlockReentry = false;
        }
    }
}
