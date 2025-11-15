using Unity.Netcode;
using UnityEngine;

public class Bomb : InteractiveItem
{
    [Header("Bomb Settings")]
    public float explosionRadius = 3f;
    public float explosionForce = 10f;
    public GameObject explosionEffectPrefab; // 파티클 프리팹

    protected override void ActivateItem()
    {
        // 1. 시각 효과 (ClientRPC로 모든 클라이언트에게 재생)
        SpawnEffectClientRpc(transform.position);

        // 2. 범위 내 물리 효과 (서버 처리)
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in colliders)
        {
            // 플레이어 넉백 처리
            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc != null)
            {
                // PlayerController에 넉백 함수가 없다면 Rigidbody에 직접 가함
                Rigidbody pcRb = pc.GetComponent<Rigidbody>();
                if (pcRb != null && !pcRb.isKinematic)
                {
                    pcRb.AddExplosionForce(explosionForce, transform.position, explosionRadius, 1f, ForceMode.Impulse);
                }
            }
        }

        // 3. 부모 클래스의 로직 실행 (Despawn 등)
        base.ActivateItem();
    }

    [ClientRpc]
    private void SpawnEffectClientRpc(Vector3 pos)
    {
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, pos, Quaternion.identity);
        }
    }
}
