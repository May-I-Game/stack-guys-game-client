using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class JiggleBallTrap : NetworkBehaviour
{
    [Header("Rotate Settings")]
    public float rightZ = 60f;

    public float leftZ = -60f;

    public float rotationSpeed = 50f;

    public float stayTime = 0.5f;

    public bool autoRotate = true;

    void Start()
    {
        if (IsServer)
        {
            if (autoRotate)
            {
                StartCoroutine(AutoRotateRoutine());
            }
        }
    }

    private IEnumerator AutoRotateRoutine()
    {
        while (true)
        {
            yield return RotateTo(leftZ);
            yield return new WaitForSeconds(stayTime);

            yield return RotateTo(rightZ);
            yield return new WaitForSeconds(stayTime);
        }
    }

    private IEnumerator RotateTo(float targetZ)
    {
        Quaternion startRotation = transform.localRotation;
        Quaternion targetRotation = Quaternion.Euler(
            transform.localEulerAngles.x,
            transform.localEulerAngles.y,
            targetZ
        );

        float startZ = transform.localEulerAngles.z;
        float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(startZ, targetZ));
        float duration = deltaAngle / rotationSpeed;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.localRotation = targetRotation;
    }
}
