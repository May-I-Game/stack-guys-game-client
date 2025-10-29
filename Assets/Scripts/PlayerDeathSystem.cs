using System.Collections;
using UnityEngine;

public class PlayerDeathSystem : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("리스폰 대기 시간")]
    public float respawnDelay = 2f;

    [Header("Corpse Settings")]
    [Tooltip("시체 프리팹 (비워두면 자동 생성)")]
    public GameObject corpsePrefab;

    [Tooltip("시체가 사라지는 시간")]
    public float corpseLifetime = 10f;

    private CharacterController characterController;
    private bool isDead = false;
    private Vector3 spawnPosition;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        // 시작 위치를 스폰 포인트로 저장
        spawnPosition = transform.position;
    }

    public void Die()
    {
        if (isDead) return;

        isDead = true;

        // 시체 생성
        CreateCorpse();

        // 플레이어 숨기기
        HidePlayer();

        // ThirdPersonController 비활성화
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // 리스폰 시작
        StartCoroutine(RespawnCoroutine());
    }

    void CreateCorpse()
    {
        if (corpsePrefab != null)
        {
            // 프리팹이 있는 경우
            GameObject corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);
            Destroy(corpse, corpseLifetime);
        }
        else
        {
            // 프리팹이 없는 경우 - 플레이어 복제
            GameObject corpse = Instantiate(gameObject, transform.position, transform.rotation);
            corpse.name = "PlayerCorpse";

            // 불필요한 컴포넌트 제거
            Destroy(corpse.GetComponent<StarterAssets.ThirdPersonController>());
            Destroy(corpse.GetComponent<StarterAssets.StarterAssetsInputs>());
            Destroy(corpse.GetComponent<PlayerDeathSystem>());
            Destroy(corpse.GetComponent<CharacterController>());

#if ENABLE_INPUT_SYSTEM
            var playerInput = corpse.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                Destroy(playerInput);
            }
#endif

            // Rigidbody 추가 (물리 효과)
            Rigidbody rb = corpse.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = corpse.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;

            // Collider 확인 및 추가
            Collider col = corpse.GetComponent<Collider>();
            if (col == null)
            {
                CapsuleCollider capsule = corpse.AddComponent<CapsuleCollider>();
                capsule.height = 2f;
                capsule.radius = 0.5f;
                capsule.center = new Vector3(0, 1f, 0);
            }

            // 시체 태그 변경
            corpse.tag = "Corpse";

            // 일정 시간 후 제거
            Destroy(corpse, corpseLifetime);
        }
    }

    void HidePlayer()
    {
        // 렌더러 비활성화
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            rend.enabled = false;
        }

        // Collider 비활성화
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    void ShowPlayer()
    {
        // 렌더러 활성화
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            rend.enabled = true;
        }

        // Collider 활성화
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = true;
        }
    }

    IEnumerator RespawnCoroutine()
    {
        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(respawnDelay);

        // CharacterController 일시 비활성화 (위치 이동을 위해)
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // 스폰 위치로 이동
        transform.position = spawnPosition;
        transform.rotation = Quaternion.identity;

        // CharacterController 다시 활성화
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // 플레이어 다시 보이게 함
        ShowPlayer();

        // ThirdPersonController 활성화
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = true;
        }

        isDead = false;
    }

    // PressTrap에서 호출할 수 있는 공개 메서드
    public bool IsDead()
    {
        return isDead;
    }

    // 스폰 위치를 변경하고 싶을 때 사용
    public void SetSpawnPosition(Vector3 newSpawnPosition)
    {
        spawnPosition = newSpawnPosition;
    }
}