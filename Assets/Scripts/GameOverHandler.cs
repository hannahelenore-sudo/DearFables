using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ends the game when the player's health reaches 0 (PlayerHealth.Died).
/// No setup: it creates itself when the game starts and stays alive across scenes.
/// Shows a simple GAME OVER screen with a Try Again button (or press Enter).
/// </summary>
public class GameOverHandler : MonoBehaviour
{
    // Scene to load on Try Again. Leave empty to use the first scene in Build Settings (usually the title screen).
    public const string RestartSceneName = "";

    private static GameOverHandler instance;
    private bool showing;
    private GUIStyle titleStyle, buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("GameOverHandler");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameOverHandler>();
    }

    private void OnEnable()
    {
        PlayerHealth.Died += OnDied;
    }

    private void OnDisable()
    {
        PlayerHealth.Died -= OnDied;
    }

    private void OnDied()
    {
        if (showing) return;
        showing = true;
        Time.timeScale = 0f;
        Debug.Log("GAME OVER: the player ran out of health cards.");
    }

    private void Update()
    {
        if (showing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Restart();
    }

    private void Restart()
    {
        showing = false;

        PlayerHealth.ResetAll();
        MoodManager.Clear();
        PlayerStamina.Refill();

        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(RestartSceneName)) SceneManager.LoadScene(RestartSceneName);
        else SceneManager.LoadScene(0);
    }

    private void OnGUI()
    {
        if (!showing) return;

        GUI.depth = -1000;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }

        titleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.12f);
        buttonStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.04f);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, Screen.height * 0.2f), "GAME OVER", titleStyle);

        float w = Screen.width * 0.2f, h = Screen.height * 0.09f;
        if (GUI.Button(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.55f, w, h), "Try Again", buttonStyle))
            Restart();
    }
}
