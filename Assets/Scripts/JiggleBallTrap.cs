using System.Collections;
using UnityEngine;

public class JiggleBallTrap : MonoBehaviour
{
    [Header("Rotate Settings")]
    [Tooltip("오른쪽으로 회전할 각도")]
    public float rightZ = 60f;

    [Tooltip("왼쪽으로 회전할 각도")]
    public float leftZ = -60f;

    [Tooltip("회전 속도 (도/초)")]
    public float rotationSpeed = 50f;

    [Tooltip("정지 시간 (초)")]
    public float stayTime = 0.5f;

    [Tooltip("자동으로 계속 왕복 회전")]
    public bool autoRotate = true;

    void Start()
    {
        if (autoRotate)
        {
            StartCoroutine(AutoRotateRoutine());
        }
    }

    private IEnumerator AutoRotateRoutine()
    {
        while (true)
        {
            // 왼쪽으로 회전
            yield return RotateTo(leftZ);
            yield return new WaitForSeconds(stayTime);

            // 오른쪽으로 회전
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
