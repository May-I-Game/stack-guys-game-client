using System.Collections;
using UnityEngine;

public class PlayerDeathSystem : MonoBehaviour
{
    [Header("Respawn Settings")]
    public float respawnDelay = 2f;

    [Header("Corpse Settings")]
    public GameObject corpsePrefab;

    public float corpseLifetime = 10f;

    private CharacterController characterController;
    private bool isDead = false;
    private Vector3 spawnPosition;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        spawnPosition = transform.position;
    }

    public void Die()
    {
        if (isDead) return;

        isDead = true;

        CreateCorpse();

        HidePlayer();

        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        StartCoroutine(RespawnCoroutine());
    }

    // 시체 없이 즉시 리스폰
    public void Die(string killerTag)
    {
        if (isDead) return;

        isDead = true;

        HidePlayer();

        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        RespawnNow();
    }

    void CreateCorpse()
    {
        if (corpsePrefab != null)
        {
            GameObject corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);
            Destroy(corpse, corpseLifetime);
        }
        else
        {
            GameObject corpse = Instantiate(gameObject, transform.position, transform.rotation);
            corpse.name = "PlayerCorpse";

            Destroy(corpse.GetComponent<StarterAssets.ThirdPersonController>());
            Destroy(corpse.GetComponent<StarterAssets.StarterAssetsInputs>());
            Destroy(corpse.GetComponent<PlayerDeathSystem>());
            Destroy(corpse.GetComponent<CharacterController>());

            // 이름표 제거
            NameTag nameTag = corpse.GetComponentInChildren<NameTag>();
            if (nameTag != null)
            {
                Destroy(nameTag.gameObject);
            }

#if ENABLE_INPUT_SYSTEM
            var playerInput = corpse.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                Destroy(playerInput);
            }
#endif

            Collider[] existingColliders = corpse.GetComponents<Collider>();
            foreach (Collider col in existingColliders)
            {
                Destroy(col);
            }

            CapsuleCollider capsule = corpse.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0, 1f, 0);

            Rigidbody rb = corpse.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = corpse.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;
            rb.mass = 70f; 
            rb.linearDamping = 0.5f; 
            rb.angularDamping = 0.5f; 
            
            // 시체가 Ground 레이어와 충돌하도록 설정
            
            // 시체 레이어를 Default로 설정 (Ground와 충돌 가능)
            corpse.layer = LayerMask.NameToLayer("Default");

            // 자식 오브젝트들도 레이어 변경
            foreach (Transform child in corpse.GetComponentsInChildren<Transform>())
            {
                child.gameObject.layer = LayerMask.NameToLayer("Default");
            }

            // 시체 태그 변경
            corpse.tag = "Corpse";

            // 일정 시간 후 제거
            //Destroy(corpse, corpseLifetime);
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

    public void RespawnNow()
    {
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

        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = true;
        }

        isDead = false;
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

        // ThirdPersonController Ȱ��ȭ
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = true;
        }

        isDead = false;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public void SetSpawnPosition(Vector3 newSpawnPosition)
    {
        spawnPosition = newSpawnPosition;
    }
}