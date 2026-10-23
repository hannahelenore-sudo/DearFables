using UnityEngine;
// You must include this namespace to manage scenes
using UnityEngine.SceneManagement;

public class ZoneLoadScene : MonoBehaviour
{
    // Type the exact name of the scene you want to load in the inspector
    [SerializeField] private string CardGame;

    // This built-in Unity function detects when a 3D collider enters the trigger
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the zone has the "Player" tag
        if (other.CompareTag("Player"))
        {
            // Load the specified scene
            SceneManager.LoadScene(CardGame);
        }
    }
}
