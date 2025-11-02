using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PressTrap : MonoBehaviour
{
    [Header("Press Settings")]
    public float upY = 2f;                 // 올라간 위치 (절대좌표)
    public float downY = 0.2f;             // 내려간 위치 (절대좌표)
    public float speed = 5f;               // 이동 속도
    public float stayDownTime = 0.5f;      // 눌린 상태 유지 시간
    public Vector2 randomDelayRange = new Vector2(1f, 4f); // 랜덤 타이밍

    private void Start()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // 무한 루프로 랜덤 타이밍 작동
            StartCoroutine(RandomPressLoop());
        }
    }

    private IEnumerator RandomPressLoop()
    {
        while (GameManager.instance.IsLobby)
        {
            yield return null;
        }

        while (GameManager.instance.IsGame)
        {
            float waitTime = Random.Range(randomDelayRange.x, randomDelayRange.y);
            yield return new WaitForSeconds(waitTime);

            yield return PressRoutine();
        }
    }

    private IEnumerator PressRoutine()
    {
        // 내려가기
        yield return MovePress(downY);

        // 눌린 상태 유지
        yield return new WaitForSeconds(stayDownTime);

        // 올라가기
        yield return MovePress(upY);
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
        if (!NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            // 리스폰 인덱스를 이용한 텔레포트
            player.DoRespawn(player.RespawnId.Value);
        }
    }
}
