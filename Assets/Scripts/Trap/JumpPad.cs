using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(BoxCollider))]
public class JumpPad : NetworkBehaviour
{
    [Header("점프 설정")]
    [SerializeField] private float launchForce = 20f;   // 발사 힘
    // [SerializeField] private float launchAngle = 45f;   // 발사 각도

    [Header("쿨다운 설정")]
    [SerializeField] private float cooldownTime = 0.5f; // 연속 발동 방지
    private float lastLaunchTime = -999f;

    private BoxCollider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
    }

    // 플레이어 충돌 감지 및 점프 실행
    private void OnTriggerEnter(Collider other)
    {
        // 서버만 물리 처리 (클라이언트는 Trigger 감지만, 물리는 서버 권위)
        if (!IsServer)
            return;

        if (Time.time - lastLaunchTime < cooldownTime)
            return;

        if (!other.CompareTag("Player"))
            return;

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        // 서버에서만 물리 적용
        LaunchPlayer(rb);
        lastLaunchTime = Time.time;
    }

    // 플레이어에게 발사 힘 적용
    private void LaunchPlayer(Rigidbody rb)
    {
        rb.linearVelocity = Vector3.zero;
        Vector3 direction = transform.up;

        rb.AddForce(direction * launchForce, ForceMode.VelocityChange);
    }
}
