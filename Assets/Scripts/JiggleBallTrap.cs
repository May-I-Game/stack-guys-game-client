using System.Collections;
using UnityEngine;

public class JiggleBallTrap : MonoBehaviour
{
    [Header("Rotate Settings")]
    [Tooltip("���������� ȸ���� ����")]
    public float rightZ = 60f;

    [Tooltip("�������� ȸ���� ����")]
    public float leftZ = -60f;

    [Tooltip("ȸ�� �ӵ� (��/��)")]
    public float rotationSpeed = 50f;

    [Tooltip("���� �ð� (��)")]
    public float stayTime = 0.5f;

    [Tooltip("�ڵ����� ��� �պ� ȸ��")]
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
            // �������� ȸ��
            yield return RotateTo(leftZ);
            yield return new WaitForSeconds(stayTime);

            // ���������� ȸ��
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
