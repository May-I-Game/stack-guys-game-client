using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;


// 잡을 수 있는 오브젝트에 붙이는 컴포넌트
public class GrabbableObject : NetworkBehaviour
{
    public NetworkVariable<bool> netIsGrabbed = new NetworkVariable<bool>(false);
    public PlayerController holder = null;

    // 시체 NetworkTransform 최적화 (Inspector에서 disabled 상태로 시작)
    private NetworkTransform networkTransform;
    private Rigidbody rb;

    private void Start()
    {
        networkTransform = GetComponent<NetworkTransform>();
        rb = GetComponent<Rigidbody>();

        // NetworkTransform은 Inspector에서 disabled 상태로 설정되어야 함
        // 시체는 기본적으로 동기화 OFF
        if (networkTransform != null)
        {
            networkTransform.enabled = false;
        }
    }

    // 잡았을 때 호출 (PlayerController에서 호출)
    public void OnGrabbed()
    {
        if (!IsServer) return;

        // NetworkTransform 활성화 (잡혀있는 동안 동기화)
        if (networkTransform != null)
        {
            networkTransform.enabled = true;
            Debug.Log($"[GrabbableObject] Grabbed, NetworkTransform enabled");
        }

        // Rigidbody Kinematic으로 (잡혀있는 동안은 물리 정지)
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    // 던졌을 때 호출 (PlayerController에서 호출)
    public void OnThrown()
    {
        if (!IsServer) return;

        // NetworkTransform 활성화 유지 (날아가는 동안 동기화 필요)
        if (networkTransform != null)
        {
            networkTransform.enabled = true;
        }

        // Rigidbody 물리 활성화 (던져진 후 날아감)
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // 1초 후 NetworkTransform 비활성화 (정착 후 불필요)
        Invoke(nameof(DisableNetworkTransformAfterSettle), 1f);
    }

    // 정착 후 NetworkTransform 비활성화
    private void DisableNetworkTransformAfterSettle()
    {
        if (!IsServer) return;

        // 다시 잡히지 않았고, NetworkTransform이 활성화되어 있으면 비활성화
        if (!netIsGrabbed.Value && networkTransform != null && networkTransform.enabled)
        {
            networkTransform.enabled = false;
            Debug.Log($"[GrabbableObject] Settled, NetworkTransform disabled");

            // Rigidbody Kinematic으로 (물리 최적화)
            if (rb != null)
            {
                rb.isKinematic = true;
            }
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