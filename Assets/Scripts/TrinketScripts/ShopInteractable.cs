using UnityEngine;
using TMPro;

/// <summary>
/// Put this on the shop GameObject in the world (the car).
/// - The shop opens ONLY by pressing the interact key (E) while the player is near.
/// - A "Press E to interact" prompt floats above the shopkeeper while the player is in range.
/// - While the shop is open the player can't do anything (walk, run, drag, jump, dash), and the camera peek is paused.
/// </summary>
public class ShopInteractable : MonoBehaviour
{
    [Header("Shop")]
    public TrinketShopUI shopUI;

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    [Tooltip("Optional. The distance is measured from the object that has the PlayerMovement script (found automatically). This is only used if no PlayerMovement exists.")]
    public Transform player;
    [Tooltip("How close the player must be to the car (flat distance, height is ignored).")]
    public float interactRange = 4f;
    [Tooltip("Writes the measured distance to the Console every time you press the interact key. Handy to check the range.")]
    public bool logDistanceOnPress = true;

    [Header("'Press E' Prompt")]
    [Tooltip("The shopkeeper. The prompt floats above this. Leave empty to float above the car.")]
    public Transform shopkeeper;
    [Tooltip("Optional: your own prompt object (for example an E key sprite). Leave empty to auto-create a text prompt.")]
    public GameObject customPrompt;
    [Tooltip("How far above the shopkeeper's pivot the prompt floats (auto-created prompt only).")]
    public Vector3 promptOffset = new Vector3(0f, 2.6f, 0f);
    public float promptFontSize = 4f;
    public float promptBobHeight = 0.08f;
    public float promptBobSpeed = 3f;
    [Tooltip("How fast the prompt pops in and out.")]
    public float promptPopSpeed = 14f;

    [Header("While Shopping")]
    [Tooltip("Any other player scripts that should be switched off while the shop is open (throwing, building...).")]
    public Behaviour[] extraScriptsToDisable;
    [Tooltip("Pause the camera sneak peek while the shop is open, so the camera doesn't slide when you move the mouse over the shop.")]
    public bool pauseCameraPeek = true;

    private PlayerMovement playerMovement;
    private CameraFollow cameraFollow;
    private bool lockedForShop;
    private bool peekWasEnabled;
    private bool[] extraWasEnabled;
    private bool warnedNoPlayer;
    private float nextFindTime;

    // The object that really moves is the one with PlayerMovement on it
    private Transform PlayerTransform
    {
        get { return playerMovement != null ? playerMovement.transform : player; }
    }

    private Transform promptRoot;
    private Vector3 promptBaseScale = Vector3.one;
    private bool autoPrompt;
    private float promptScale;

    private void Start()
    {
        FindPlayer();
        BuildPrompt();
    }

    private void Update()
    {
        if (playerMovement == null && Time.unscaledTime >= nextFindTime)
        {
            nextFindTime = Time.unscaledTime + 1f;
            FindPlayer();
        }

        bool open = shopUI != null && shopUI.IsOpen;

        // Keeps the player frozen exactly as long as the shop is open (however it was closed)
        if (open != lockedForShop) SetPlayerLocked(open);

        bool inRange = IsPlayerInRange();

        if (!open && Input.GetKeyDown(interactKey))
        {
            if (logDistanceOnPress)
            {
                Transform p = PlayerTransform;
                Debug.Log(p == null
                    ? "ShopInteractable: " + interactKey + " pressed but no player was found."
                    : "ShopInteractable: " + interactKey + " pressed. Measuring from '" + p.name + "', " +
                      FlatDistance(p.position, transform.position).ToString("0.0") + " away from the car (range " + interactRange + ") -> " +
                      (inRange ? "opening shop" : "too far"), this);
            }

            if (inRange) TryOpen();
        }

        UpdatePrompt(inRange && !(shopUI != null && shopUI.IsOpen));
    }

    private void OnDisable()
    {
        if (lockedForShop) SetPlayerLocked(false);
    }

    private void OnDestroy()
    {
        if (autoPrompt && promptRoot != null) Destroy(promptRoot.gameObject);
    }

    // -------------------------------------------------------------------
    // Opening
    // -------------------------------------------------------------------

    private void TryOpen()
    {
        if (shopUI == null)
        {
            shopUI = Object.FindAnyObjectByType<TrinketShopUI>();
            if (shopUI == null)
            {
                Debug.LogWarning("ShopInteractable: no TrinketShopUI found. Use Tools > Shop > Create Shop UI.", this);
                return;
            }
        }

        shopUI.Open();
        if (shopUI.IsOpen) SetPlayerLocked(true);
    }

    private void FindPlayer()
    {
        if (playerMovement == null) playerMovement = Object.FindAnyObjectByType<PlayerMovement>();

        if (playerMovement == null && player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }

        if (PlayerTransform == null && !warnedNoPlayer)
        {
            Debug.LogWarning("ShopInteractable: no player found (no PlayerMovement in the scene and no object tagged 'Player').", this);
            warnedNoPlayer = true;
        }
    }

    private bool IsPlayerInRange()
    {
        Transform p = PlayerTransform;
        if (p == null) return false;
        if (interactRange <= 0f) return true;
        return FlatDistance(p.position, transform.position) <= interactRange;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // -------------------------------------------------------------------
    // Freeze the player while shopping
    // -------------------------------------------------------------------

    private void SetPlayerLocked(bool locked)
    {
        lockedForShop = locked;

        // Walk, run, mouse-drag, jump, dash are all blocked inside PlayerMovement
        if (playerMovement != null) playerMovement.InputLocked = locked;

        // Any other player scripts
        if (extraScriptsToDisable != null)
        {
            if (extraWasEnabled == null || extraWasEnabled.Length != extraScriptsToDisable.Length)
                extraWasEnabled = new bool[extraScriptsToDisable.Length];

            for (int i = 0; i < extraScriptsToDisable.Length; i++)
            {
                Behaviour b = extraScriptsToDisable[i];
                if (b == null) continue;

                if (locked)
                {
                    extraWasEnabled[i] = b.enabled;
                    b.enabled = false;
                }
                else
                {
                    b.enabled = extraWasEnabled[i];
                }
            }
        }

        // Camera sneak peek
        if (pauseCameraPeek)
        {
            if (cameraFollow == null) cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null)
            {
                if (locked)
                {
                    peekWasEnabled = cameraFollow.enablePeek;
                    cameraFollow.enablePeek = false;
                }
                else
                {
                    cameraFollow.enablePeek = peekWasEnabled;
                }
            }
        }
    }

    // -------------------------------------------------------------------
    // "Press E" prompt above the shopkeeper
    // -------------------------------------------------------------------

    private void BuildPrompt()
    {
        if (customPrompt != null)
        {
            promptRoot = customPrompt.transform;
            promptBaseScale = promptRoot.localScale;
            autoPrompt = false;
        }
        else
        {
            string key = interactKey.ToString();

            GameObject go = new GameObject("InteractPrompt");
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            t.text = "<size=170%><b><color=#FFD84A>" + key + "</color></b></size>\n<size=65%>Press " + key + " to interact</size>";
            t.fontSize = promptFontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 255);
            t.sortingOrder = 200; // draw on top of sprites
            t.rectTransform.sizeDelta = new Vector2(10f, 3f);

            promptRoot = go.transform;
            promptBaseScale = Vector3.one;
            autoPrompt = true;
        }

        promptScale = 0f;
        promptRoot.gameObject.SetActive(false);
    }

    private void UpdatePrompt(bool show)
    {
        if (promptRoot == null) return;

        float target = show ? 1f : 0f;
        promptScale = Mathf.Lerp(promptScale, target, 1f - Mathf.Exp(-promptPopSpeed * Time.unscaledDeltaTime));

        if (!show && promptScale < 0.01f)
        {
            if (promptRoot.gameObject.activeSelf) promptRoot.gameObject.SetActive(false);
            return;
        }

        if (!promptRoot.gameObject.activeSelf) promptRoot.gameObject.SetActive(true);
        promptRoot.localScale = promptBaseScale * promptScale;

        if (autoPrompt)
        {
            Transform anchor = shopkeeper != null ? shopkeeper : transform;
            float bob = Mathf.Sin(Time.unscaledTime * promptBobSpeed) * promptBobHeight;
            promptRoot.position = anchor.position + promptOffset + Vector3.up * bob;

            Camera cam = Camera.main;
            if (cam != null) promptRoot.rotation = cam.transform.rotation; // always faces the camera
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (interactRange > 0f)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }

        Transform anchor = shopkeeper != null ? shopkeeper : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anchor.position + promptOffset, 0.15f);
    }
}