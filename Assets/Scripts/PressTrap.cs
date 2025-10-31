using System.Collections;
using UnityEngine;

public class PressTrap : MonoBehaviour
{
    [Header("Press Settings")]
    public float upY = 2f;                 // 올라간 위치 (절대좌표)
    public float downY = 0.2f;             // 내려간 위치 (절대좌표)
    public float speed = 5f;               // 이동 속도
    public float stayDownTime = 0.5f;      // 눌린 상태 유지 시간
    public Vector2 randomDelayRange = new Vector2(1f, 4f); // 랜덤 타이밍

    private bool isPressing = false;

    private void Start()
    {
        // 무한 루프로 랜덤 타이밍 작동
        StartCoroutine(RandomPressLoop());
    }

    private IEnumerator RandomPressLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(randomDelayRange.x, randomDelayRange.y);
            yield return new WaitForSeconds(waitTime);

            yield return PressRoutine();
        }
    }

    private IEnumerator PressRoutine()
    {
        isPressing = true;

        // 내려가기
        yield return MovePress(downY);

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerDeathSystem deathSystem = other.GetComponent<PlayerDeathSystem>();
            if (deathSystem != null && !deathSystem.IsDead())
            {
                deathSystem.Die();
            }
        }
    }
}
