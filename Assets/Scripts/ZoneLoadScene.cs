using UnityEngine;

public class ZoneLoadScene : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

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