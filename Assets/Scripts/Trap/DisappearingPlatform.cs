using UnityEngine;
using System.Collections;

public class RespawningPlatformTrigger : MonoBehaviour
{
    // 발판이 사라지기까지의 지연 시간 
    public float delayBeforeDisappear = 0.5f;

    // 발판이 사라진 후 다시 나타나기까지의 시간
    public float respawnTime = 3f;

    private Collider platformCollider;
    private MeshRenderer platformRenderer;

    void Start()
    {
        platformCollider = GetComponent<Collider>();
        platformRenderer = GetComponent<MeshRenderer>();

        if (platformCollider == null || platformRenderer == null)
        {
            Debug.LogError("발판에 Collider 또는 MeshRenderer 컴포넌트가 없습니다!", this);
        }
    }

    // ⭐⭐⭐ OnCollisionEnter 대신 OnTriggerEnter를 사용합니다. ⭐⭐⭐
    private void OnTriggerEnter(Collider other)
    {
        // 'other'는 충돌한 오브젝트의 Collider입니다.
        // 충돌한 오브젝트의 태그가 "Player"인지 확인합니다.
        StartCoroutine(DisappearAndRespawn());
    }

    IEnumerator DisappearAndRespawn()
    {
        // 1. 발판이 사라지기 전의 지연 시간
        yield return new WaitForSeconds(delayBeforeDisappear);

        // 2. 발판을 비활성화(사라지게) 합니다.
        platformCollider.enabled = false; // 충돌 감지 비활성화 (플레이어가 계속 밟고 있어도 다시 감지하지 않도록)
        platformRenderer.enabled = false; // 시각적으로 숨김

        // 3. 발판이 사라진 상태로 리스폰 시간만큼 기다립니다.
        yield return new WaitForSeconds(respawnTime);

        // 4. 발판을 다시 활성화(나타나게) 합니다.
        platformCollider.enabled = true; // 충돌 감지 활성화
        platformRenderer.enabled = true; // 시각적으로 표시
    }
}