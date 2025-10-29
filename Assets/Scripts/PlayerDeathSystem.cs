using System.Collections;
using UnityEngine;

public class PlayerDeathSystem : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("������ ��� �ð�")]
    public float respawnDelay = 2f;

    [Header("Corpse Settings")]
    [Tooltip("��ü ������ (����θ� �ڵ� ����)")]
    public GameObject corpsePrefab;

    [Tooltip("��ü�� ������� �ð�")]
    public float corpseLifetime = 10f;

    private CharacterController characterController;
    private bool isDead = false;
    private Vector3 spawnPosition;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        // ���� ��ġ�� ���� ����Ʈ�� ����
        spawnPosition = transform.position;
    }

    public void Die()
    {
        if (isDead) return;

        isDead = true;

        // ��ü ����
        CreateCorpse();

        // �÷��̾� �����
        HidePlayer();

        // ThirdPersonController ��Ȱ��ȭ
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // ������ ����
        StartCoroutine(RespawnCoroutine());
    }

    void CreateCorpse()
    {
        if (corpsePrefab != null)
        {
            // �������� �ִ� ���
            GameObject corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);
            Destroy(corpse, corpseLifetime);
        }
        else
        {
            // �������� ���� ��� - �÷��̾� ����
            GameObject corpse = Instantiate(gameObject, transform.position, transform.rotation);
            corpse.name = "PlayerCorpse";

            // ���ʿ��� ������Ʈ ����
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

            // Rigidbody �߰� (���� ȿ��)
            Rigidbody rb = corpse.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = corpse.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;

            // Collider Ȯ�� �� �߰�
            Collider col = corpse.GetComponent<Collider>();
            if (col == null)
            {
                CapsuleCollider capsule = corpse.AddComponent<CapsuleCollider>();
                capsule.height = 2f;
                capsule.radius = 0.5f;
                capsule.center = new Vector3(0, 1f, 0);
            }

            // ��ü �±� ����
            corpse.tag = "Corpse";

            // ���� �ð� �� ����
            Destroy(corpse, corpseLifetime);
        }
    }

    void HidePlayer()
    {
        // ������ ��Ȱ��ȭ
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            rend.enabled = false;
        }

        // Collider ��Ȱ��ȭ
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    void ShowPlayer()
    {
        // ������ Ȱ��ȭ
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            rend.enabled = true;
        }

        // Collider Ȱ��ȭ
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = true;
        }
    }

    IEnumerator RespawnCoroutine()
    {
        // ������ �ð���ŭ ���
        yield return new WaitForSeconds(respawnDelay);

        // CharacterController �Ͻ� ��Ȱ��ȭ (��ġ �̵��� ����)
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // ���� ��ġ�� �̵�
        transform.position = spawnPosition;
        transform.rotation = Quaternion.identity;

        // CharacterController �ٽ� Ȱ��ȭ
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // �÷��̾� �ٽ� ���̰� ��
        ShowPlayer();

        // ThirdPersonController Ȱ��ȭ
        var controller = GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = true;
        }

        isDead = false;
    }

    // PressTrap���� ȣ���� �� �ִ� ���� �޼���
    public bool IsDead()
    {
        return isDead;
    }

    // ���� ��ġ�� �����ϰ� ���� �� ���
    public void SetSpawnPosition(Vector3 newSpawnPosition)
    {
        spawnPosition = newSpawnPosition;
    }
}