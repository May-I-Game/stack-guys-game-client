using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

public class RespawnManager : NetworkBehaviour
{
    [Header("리스폰 좌표 오브젝트")]
    public List<Transform> respawnPoints = new();

    [Header("플레이어 텔레포트 함수명 (Vector3, Quaternion)")]
    public string teleportMethodName = "DoRespawn";

    private void Awake()
    {
        var no = GetComponent<NetworkObject>();
    }

    // 인덱스를 이용한 리스폰
    public void RespawnTo(int index)
    {
        if (index < 0 || index >= respawnPoints.Count) { Debug.LogWarning("[RespawnMgr] Invalid index"); return; }
        var dest = respawnPoints[index]; if (!dest) { Debug.LogWarning("[RespawnMgr] Null dest"); return; }

        // 프리플라이트 체크(클라에서만 ServerRpc 전송 가능)
        if (NetworkManager == null || !NetworkManager.IsListening)
        {
            Debug.LogWarning("[RespawnMgr] 네트워크 매니저 문제");
            return;
        }
        if (!IsClient)
        {
            Debug.LogWarning("[RespawnMgr] 클라이언트가 아닙니다");
            return;
        }
        if (!IsSpawned)
        {
            Debug.LogWarning("[RespawnMgr] 스폰 X");
            return;
        }

        RequestTeleportServerRpc(index); // 여기까지는 클라 측
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTeleportServerRpc(int index, ServerRpcParams rpcParams = default)
    {
        if (index < 0 || index >= respawnPoints.Count) { Debug.LogWarning("[RespawnMgr/SERVER] 잘못된 인덱스"); return; }
        var dest = respawnPoints[index]; if (!dest) { Debug.LogWarning("[RespawnMgr/SERVER] Null dest"); return; }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client)) return;
       
        var playerObj = client.PlayerObject;
        if (!playerObj) { Debug.LogWarning("[RespawnMgr/SERVER] 플레이어 오브젝트 없음"); return; }

        bool ok = TryInvokeTeleportOn(playerObj.gameObject, teleportMethodName, dest.position, dest.rotation);
        Debug.Log(ok
            ? $"[RespawnMgr/SERVER] Teleport 호출 → {dest.name}"
            : $"[RespawnMgr/SERVER] Teleport 함수 찾을 수 없음 '{teleportMethodName}(Vector3,Quaternion)' 무시됨");
    }

    private bool TryInvokeTeleportOn(GameObject go, string method, Vector3 pos, Quaternion rot)
    {
        if (!go || string.IsNullOrWhiteSpace(method)) return false;
        var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in behaviours)
        {
            if (!mb) continue;
            var mi = mb.GetType().GetMethod(
                method, BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(Vector3), typeof(Quaternion) }, null);
            if (mi != null)
            {
                try { mi.Invoke(mb, new object[] { pos, rot }); return true; }
                catch (Exception e) { Debug.LogWarning($"[RespawnMgr/SERVER] 함수 호출 실패: {e.Message}"); return false; }
            }
        }
        return false;
    }
}
