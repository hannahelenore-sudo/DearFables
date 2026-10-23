using UnityEngine;

public class GrassSway : MonoBehaviour
{
    public float swayAmount = 8f;
    public float swaySpeed = 2f;

    public float stepBendAmount = 25f;
    public float returnSpeed = 8f;

    private float currentBend = 0f;
    private bool steppedOn = false;

    void Update()
    {
        float idleSway = Mathf.Sin(Time.time * swaySpeed) * swayAmount;

        if (steppedOn)
        {
            currentBend = Mathf.Lerp(currentBend, 0f, returnSpeed * Time.deltaTime);

            if (Mathf.Abs(currentBend) < 0.1f)
            {
                currentBend = 0f;
                steppedOn = false;
            }
        }

        transform.rotation = Quaternion.Euler(0, 0, idleSway + currentBend);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            currentBend = Random.Range(-stepBendAmount, stepBendAmount);
            steppedOn = true;
        }
    }
}