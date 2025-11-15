using Unity.Netcode;
using UnityEngine;

public class Bomb : InteractiveItem
{
    [Header("Bomb Settings")]
    public float explosionRadius = 3f;
    public float explosionForce = 10f;
    public GameObject explosionEffectPrefab; // 파티클 프리팹

    private bool hasExploded = false;

    protected override void OnCollisionEnter(Collision collision)
    {
        //Debug.Log($"[Bomb] 충돌 감지! 대상: {collision.gameObject.name}");
        //Debug.Log($"[Bomb] IsGrabbed: {IsGrabbed}, wasThrown 확인 필요");

        base.OnCollisionEnter(collision);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 서버만 물리 활성화 (던질 수 있게)
        if (IsServer)
        {
            if (Rb != null)
            {
                Rb.isKinematic = false;     // 물리 활성화
                Rb.useGravity = true;       // 중력 활성화
            }
        }
        else
        {
            // 클라이언트는 Kinematic
            if (Rb != null)
            {
                Rb.isKinematic = true;
            }
        }
    }

    protected override void ActivateItem()
    {
        // 1. 시각 효과 (ClientRPC로 모든 클라이언트에게 재생)
        SpawnEffectClientRpc(transform.position);

        // 2. 범위 내 물리 효과 (서버 처리)
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        //Debug.Log($"[Bomb] 범위 내 충돌체 {colliders.Length}개 감지");

        foreach (Collider hit in colliders)
        {
            // 플레이어 넉백 처리
            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc != null)
            {
                // 범위 내에서 던진 사람은 충격파 제외
                if (thrower != null && pc == thrower)
                {
                    continue;
                }

                // PlayerController에 넉백 함수가 없다면 Rigidbody에 직접 가함
                Rigidbody pcRb = pc.GetComponent<Rigidbody>();
                if (pcRb != null && !pcRb.isKinematic)
                {
                    pcRb.AddExplosionForce(explosionForce, transform.position, explosionRadius, 1f, ForceMode.Impulse);
                    //Debug.Log($"[Bomb] 충격파 적용: {pc.gameObject.name}");
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
