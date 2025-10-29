using System.Collections;
using UnityEngine;

public class PressTrap : MonoBehaviour
{
    [Header("Press Settings")]
    public float upY = 2f;          // 원래 위치
    public float downY = 0.2f;      // 내려가는 위치
    public float speed = 5f;        // 이동 속도
    public float stayDownTime = 0.5f; // 눌린 상태 유지 시간

    private bool isPressing = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPressing)
        {
            PlayerDeathSystem deathSystem = other.GetComponent<PlayerDeathSystem>();

            if (deathSystem != null && !deathSystem.IsDead())
            {
                // 함정 작동 및 플레이어 죽이기
                StartCoroutine(PressRoutine(deathSystem));
            }
        }
    }

    private IEnumerator PressRoutine(PlayerDeathSystem deathSystem)
    {
        isPressing = true;

        // 내려가기
        yield return MovePress(downY);

        // 플레이어 죽이기
        if (deathSystem != null && !deathSystem.IsDead())
        {
            deathSystem.Die();
        }

        // 눌린 상태 유지
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