
using UnityEngine;

public class StarGate : MonoBehaviour
{
    public int requiredStars = 5;

    public Collider gateCollider;
    public GameObject gateVisual;

    private bool unlocked = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        CheckStars();
    }

    void CheckStars()
    {
        if (unlocked)
            return;

        if (StarManager.Instance.totalStars >= requiredStars)
        {
            unlocked = true;

            gateCollider.enabled = false;

            if (gateVisual != null)
                gateVisual.SetActive(false);

            Debug.Log("Gate Unlocked!");
        }
        else
        {
            int needed = requiredStars - StarManager.Instance.totalStars;

            Debug.Log("You need " + needed + " more stars!");
        }
    }
}
