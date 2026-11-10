using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put this on the PLAYER object in the scene you leave (the one that goes to the card game).
/// Call EnterBattle() when the player should start the card game. When this scene loads again,
/// the player is moved back to where they were.
/// Works for 2D and 3D.
/// </summary>
public class PlayerPositionSaver : MonoBehaviour
{
    [Tooltip("Exact name of the card game scene (must be in Build Profiles).")]
    public string battleSceneName = "YourCardGameSceneName";

    /// <summary>
    /// True after the player is put back inside the card game zone. The zone ignores the player
    /// until they walk out of it, so they don't get sent straight back into the card game.
    /// </summary>
    public static bool BlockReentry { get; set; }

    // Static = survives scene loads
    private static bool hasSavedPosition;
    private static Vector3 savedPosition;
    private static Quaternion savedRotation;
    private static string savedSceneName;
    private static bool savedFromZone;

    private void Start()
    {
        // Only restore if we saved in THIS scene earlier
        if (hasSavedPosition && savedSceneName == SceneManager.GetActiveScene().name)
        {
            RestorePosition();
            BlockReentry = savedFromZone;
            hasSavedPosition = false; // use it once
        }
    }

    /// <summary>Call this from your trigger / button / interaction to go to the card game.</summary>
    /// <param name="fromZone">true when started by the card game zone (default), false for enemies.</param>
    public void EnterBattle(bool fromZone = true)
    {
        savedFromZone = fromZone;
        hasSavedPosition = true;
        savedPosition = transform.position;
        savedRotation = transform.rotation;
        savedSceneName = SceneManager.GetActiveScene().name;

        SceneManager.LoadScene(battleSceneName);
    }

    private void RestorePosition()
    {
        // CharacterController overrides transform changes unless disabled first
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(savedPosition, savedRotation);

        if (cc != null) cc.enabled = true;

        // Stop any leftover movement
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Rigidbody2D rb2d = GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.linearVelocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }
    }
}