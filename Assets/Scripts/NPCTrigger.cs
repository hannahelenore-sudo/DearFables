using UnityEngine;
using UnityEngine.SceneManagement;

public class NPCTrigger : MonoBehaviour
{
    public string sceneToLoad = "NPCScene";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}