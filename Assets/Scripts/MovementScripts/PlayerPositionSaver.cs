using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerPositionSaver : MonoBehaviour
{
    [Tooltip("Exact name of the card game scene (must be in Build Profiles).")]
    public string battleSceneName = "YourCardGameSceneName";

    public static bool BlockReentry { get; set; }

    private static bool hasSavedPosition;
    private static Vector3 savedPosition;
    private static Quaternion savedRotation;
    private static string savedSceneName;
    private static bool savedFromZone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        ResetAll();
    }

    public static void ResetAll()
    {
        hasSavedPosition = false;
        savedPosition = Vector3.zero;
        savedRotation = Quaternion.identity;
        savedSceneName = null;
        savedFromZone = false;
        BlockReentry = false;
    }

    private void Start()
    {
        if (hasSavedPosition && savedSceneName == SceneManager.GetActiveScene().name)
        {
            RestorePosition();
            BlockReentry = savedFromZone;
            hasSavedPosition = false;
        }
    }

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
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(savedPosition, savedRotation);

        if (cc != null) cc.enabled = true;

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