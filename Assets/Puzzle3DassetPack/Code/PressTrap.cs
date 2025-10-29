using UnityEngine;

public class PressTrap : MonoBehaviour
{
    public Transform pressModel; // 실제로 움직일 프레스 바
    public float upY = 2f;
    public float downY = 0.2f;
    public float speed = 5f;
    public float stayDownTime = 0.5f;

    private bool isPressing = false;

    private void Start()
    {
        // 초기 위치 위로
        Vector3 pos = pressModel.localPosition;
        pos.y = upY;
        pressModel.localPosition = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPressing)
        {
            StartCoroutine(PressRoutine(other.gameObject));
        }
    }

    private System.Collections.IEnumerator PressRoutine(GameObject player)
    {
        isPressing = true;

        // 아래로 이동
        yield return MovePress(downY);

        // 압사 판정
        Collider[] hits = Physics.OverlapBox(
            pressModel.position,
            new Vector3(0.5f, 0.1f, 0.5f)
        );

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Debug.Log("플레이어 압사! 💀");
                // 플레이어 사망 처리 넣기
                // Destroy(hit.gameObject);
            }
        }

        yield return new WaitForSeconds(stayDownTime);

        // 위로 이동
        yield return MovePress(upY);

        isPressing = false;
    }

    private System.Collections.IEnumerator MovePress(float targetY)
    {
        Vector3 pos = pressModel.localPosition;

        while (Mathf.Abs(pos.y - targetY) > 0.01f)
        {
            pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * speed);
            pressModel.localPosition = pos;
            yield return null;
        }

        pos.y = targetY;
        pressModel.localPosition = pos;
    }
}
