using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;


// 잡을 수 있는 오브젝트에 붙이는 컴포넌트
public class GrabbableObject : NetworkBehaviour
{
    public NetworkVariable<bool> netIsGrabbed = new NetworkVariable<bool>(false);
    public PlayerController holder = null;

    // 시체 NetworkTransform 최적화 (Inspector에서 disabled 상태로 시작)
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 잡았을 때 호출 (PlayerController에서 호출)
    public void OnGrabbed()
    {
        if (!IsServer) return;

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

        // Rigidbody 물리 활성화 (던져진 후 날아감)
        if (rb != null)
        {
            rb.isKinematic = false;
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