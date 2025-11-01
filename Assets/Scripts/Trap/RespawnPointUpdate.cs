using Unity.Netcode;
using UnityEngine;

public class RespawnPointUpdate : NetworkBehaviour
{
    [SerializeField] public int respawnIndex = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (IsServer)
        {
            if (other.CompareTag("Player"))
            {
                var pc = other.GetComponentInParent<PlayerController>();

                // 리스폰 지역 역주행 검사 후 업데이트
                if (pc != null && respawnIndex > pc.RespawnId.Value)
                    pc.RespawnId.Value = respawnIndex;

                return;
            }
        }
    }
}