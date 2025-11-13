using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;


// 잡을 수 있는 오브젝트에 붙이는 컴포넌트
public class GrabbableObject : NetworkBehaviour
{
    public NetworkVariable<bool> netIsGrabbed = new NetworkVariable<bool>(false);
    public PlayerController holder = null;

    // 시체 NetworkTransform 최적화
    private NetworkTransform networkTransform;
    private Rigidbody rb;
    private float settleTime = 0f;
    private bool isSettled = false;
    private const float SETTLE_VELOCITY_THRESHOLD = 0.1f;
    private const float SETTLE_DURATION = 1f;

    private void Start()
    {
        networkTransform = GetComponent<NetworkTransform>();
        rb = GetComponent<Rigidbody>();

        // 시체는 처음에 동기화 OFF (떨어진 후 정착하면 자동으로 OFF)
        // 생성 시에는 ON으로 시작 (떨어지는 동안 동기화 필요)
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        // 잡혀있지 않고, 아직 정착 안 했으면 정착 체크
        if (!netIsGrabbed.Value && !isSettled && rb != null && networkTransform != null)
        {
            // 속도가 임계값 이하면 정착 타이머 증가
            if (rb.linearVelocity.magnitude < SETTLE_VELOCITY_THRESHOLD)
            {
                settleTime += Time.fixedDeltaTime;

                // 일정 시간 동안 정지 상태 = 정착
                if (settleTime >= SETTLE_DURATION)
                {
                    isSettled = true;

                    // NetworkTransform 동기화 중지 (네트워크 최적화)
                    if (networkTransform.enabled)
                    {
                        networkTransform.enabled = false;
                        Debug.Log($"[GrabbableObject] Settled, NetworkTransform disabled");
                    }

                    // Rigidbody 물리 최적화
                    rb.isKinematic = true;
                    rb.detectCollisions = false;  // 충돌 감지 완전 비활성화 (Physics 최적화)
                }
            }
            else
            {
                // 다시 움직이면 타이머 리셋
                settleTime = 0f;
            }
        }
    }

    // 잡았을 때 호출 (PlayerController에서 호출)
    public void OnGrabbed()
    {
        if (!IsServer) return;

        isSettled = false;
        settleTime = 0f;

        // NetworkTransform 동기화 켜기
        if (networkTransform != null && !networkTransform.enabled)
        {
            networkTransform.enabled = true;
            Debug.Log($"[GrabbableObject] Grabbed, NetworkTransform enabled");
        }

        // Rigidbody 설정 (잡혀있는 동안은 물리 정지, 하지만 충돌 감지는 켜기)
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = true;  // 잡혔을 때는 충돌 감지 재활성화
        }
    }

    // 던졌을 때 호출 (PlayerController에서 호출)
    public void OnThrown()
    {
        if (!IsServer) return;

        isSettled = false;
        settleTime = 0f;

        // NetworkTransform 동기화 켜기 (날아가는 동안 동기화 필요)
        if (networkTransform != null && !networkTransform.enabled)
        {
            networkTransform.enabled = true;
            Debug.Log($"[GrabbableObject] Thrown, NetworkTransform enabled");
        }

        // Rigidbody 물리 활성화 (던져진 후 날아감)
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;  // 던졌을 때 충돌 감지 활성화
        }
    }

    // 강제로 놓기 (연결해제등)
    public void ForceRelease()
    {
        if (!IsServer) return;

        if (holder != null && netIsGrabbed.Value)
        {
            // 홀더에게 놓으라고 알림
            // holder의 ThrowObjectServerRpc 호출 또는 직접 해제
            netIsGrabbed.Value = false;
            holder = null;
        }
    }
}