using Unity.Netcode;
using UnityEngine;

// 잡을 수 있는 오브젝트에 붙이는 컴포넌트
public class GrabbableObject : NetworkBehaviour, IGrabbable
{
    public bool IsGrabbed { get; private set; } = false;
    public PlayerController Holder { get; private set; } = null;
    public ulong NetId { get { return NetworkObjectId; } }
    public GameObject GameObj { get { return gameObject; } }
    public Rigidbody Rb { get; private set; }

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();

        if (Rb == null)
        {
            Debug.LogError($"[GrabbableObject] Rigidbody 컴포넌트가 없습니다: {gameObject.name}");
            enabled = false;
        }
    }

    // 잡았을 때 호출 (PlayerController에서 호출)
    public void OnGrabbed(PlayerController player)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // Rigidbody Kinematic으로 (잡혀있는 동안은 물리 정지)
        Rb.isKinematic = true;

        IsGrabbed = true;
        Holder = player;
    }

    // 던졌을 때 호출 (PlayerController에서 호출)
    public void OnThrown()
    {
        if (!IsServer) return;

        if (!IsGrabbed)
        {
            Debug.LogWarning($"[GrabbableObject] {gameObject.name}은(는) 잡혀있지 않습니다!");
            return;
        }

        OnReleased();
    }

    // 놓았을 때 호출 (PlayerController에서 호출)
    public void OnReleased()
    {
        if (!IsServer) return;

        Rb.isKinematic = false;

        IsGrabbed = false;
        Holder = null;
    }
}