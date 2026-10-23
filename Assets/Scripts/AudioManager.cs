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
        // Check if movement keys (W, A, S, D or arrows) are pressed
        if (Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0)
        {
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }

        // Play sound if moving and not already playing
        if (isMoving && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        // Stop sound when not moving
        else if (!isMoving && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
