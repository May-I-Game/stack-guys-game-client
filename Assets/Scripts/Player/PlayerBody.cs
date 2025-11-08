using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerBody : NetworkBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ocean"))
        {
            ConverToLocalClientRpc();
        }
    }

    [ClientRpc]
    private void ConverToLocalClientRpc()
    {
        ConvertToLocal();
    }

    private void ConvertToLocal()
    {
        // NetworkTransform이 있다면 비활성화
        var networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null)
        {
            networkTransform.enabled = false;
        }

        // NetworkObject 비활성화
        var netWorkObject = GetComponent<NetworkObject>();
        if (netWorkObject != null)
        {
            netWorkObject.enabled = false;
        }

        // 마지막으로 자기 자신 비활성화
        this.enabled = false;

        Debug.Log($"{this.gameObject}: 네트워크 동기화 중단, 로컬 모드로 전환");
    }
}
