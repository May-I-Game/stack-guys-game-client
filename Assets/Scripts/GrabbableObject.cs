using Unity.Netcode;
using UnityEngine;


// 잡을 수 있는 오브젝트에 붙이는 컴포넌트
public class GrabbableObject : NetworkBehaviour
{
    public NetworkVariable<bool> netIsGrabbed = new NetworkVariable<bool>(false);
    public PlayerController holder = null;

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