
using UnityEngine;
using TMPro;

public class StarManager : MonoBehaviour
{
    public static StarManager Instance;

    public int totalStars = 0;
    public TMP_Text starCounterText;

    void Awake()
    {
        Instance = this;
        UpdateUI();
    }

    public void CollectStar(int amount)
    {
        totalStars += amount;
        UpdateUI();

        Debug.Log("Stars collected: " + totalStars);
    }

    void UpdateUI()
    {
        if (starCounterText != null)
        {
            starCounterText.text = "Stars: " + totalStars;
        }
    }
}
