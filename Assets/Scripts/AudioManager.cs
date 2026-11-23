using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    private AudioSource audioSource;
    private bool isMoving;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0)
        {
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }

        if (isMoving && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if (!isMoving && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}