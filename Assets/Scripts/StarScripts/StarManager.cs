using UnityEngine;
using TMPro;

public class StarManager : MonoBehaviour
{
    public static StarManager Instance;

    public TMP_Text starCounterText;

    // Read-only. The real value lives in PlayerProgress so it survives scene loads.
    // StarGate (and anything else using StarManager.Instance.totalStars) keeps working unchanged.
    public int totalStars => PlayerProgress.TotalStars;

    void Awake()
    {
        Instance = this;
        UpdateUI();
    }

    public void CollectStar(int amount)
    {
        PlayerProgress.AddStars(amount);
        UpdateUI();

        Debug.Log("Stars collected: " + totalStars);
    }

    void UpdateUI()
    {
        if (starCounterText != null)
        {
            starCounterText.text = totalStars.ToString();
        }
    }
}