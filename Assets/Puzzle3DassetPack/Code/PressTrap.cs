using StarterAssets;
using System.Collections;
using UnityEngine;

public class PressTrap : MonoBehaviour
{
    public float upY = 2f;          // 원래 위치
    public float downY = 0.2f;      // 내려가는 위치
    public float speed = 5f;        // 이동 속도
    public float stayDownTime = 0.5f;

    private bool isPressing = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPressing)
        {
            StartCoroutine(PressRoutine(other.GetComponent<ThirdPersonController>()));
        }
    }

    private IEnumerator PressRoutine(ThirdPersonController player)
    {
        isPressing = true;

        // 내려가기
        yield return MovePress(downY);

        if (player != null)
        {
            player.Die(); // 여기서 PressTrap의 spawnPoint 사용
        }

        yield return new WaitForSeconds(stayDownTime);

        // 올라가기
        yield return MovePress(upY);

        isPressing = false;
    }

    private IEnumerator MovePress(float targetY)
    {
        Vector3 pos = transform.localPosition;

        while (Mathf.Abs(pos.y - targetY) > 0.01f)
        {
            pos.y = Mathf.MoveTowards(pos.y, targetY, speed * Time.deltaTime);
            transform.localPosition = pos;
            yield return null;
        }

        pos.y = targetY;
        transform.localPosition = pos;
    }
}
