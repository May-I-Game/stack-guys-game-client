using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct PlayerSnapshot : INetworkSerializable
{
    public ushort NetworkObjectId; // 누가 주인인가? (2 Byte)
    public ushort X, Y, Z;         // 위치 (6 Byte)
    public ushort YRotation;       // Y회전 (2 Byte) 

    // 생성자로 데이터를 넣을 때 자동 압축
    public PlayerSnapshot(ulong netId, Vector3 pos, float rotY)
    {
        NetworkObjectId = (ushort)netId; // 주의: ID 65535 넘으면 오버플로우

        X = Mathf.FloatToHalf(pos.x);
        Y = Mathf.FloatToHalf(pos.y);
        Z = Mathf.FloatToHalf(pos.z);

        float normalizedRot = Mathf.Repeat(rotY, 360f);
        YRotation = (ushort)(normalizedRot / 360f * 65535f); // 0.005도 오차
    }

    // 데이터를 꺼낼 때
    public void GetState(out ulong netId, out Vector3 pos, out float rotY)
    {
        netId = (ulong)NetworkObjectId;

        pos = new Vector3(
            Mathf.HalfToFloat(X),
            Mathf.HalfToFloat(Y),
            Mathf.HalfToFloat(Z)
        );

        rotY = (float)YRotation / 65535f * 360f;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NetworkObjectId);
        serializer.SerializeValue(ref X);
        serializer.SerializeValue(ref Y);
        serializer.SerializeValue(ref Z);
        serializer.SerializeValue(ref YRotation);
    }
}

public class BatchNetworkManager : NetworkBehaviour
{
    public static BatchNetworkManager Instance;

    // 빠른 검색을 위해 로컬 플레이어들을 캐싱해둠
    private Dictionary<ulong, PlayerController> _spawnedPlayers = new Dictionary<ulong, PlayerController>();

    private void Awake() => Instance = this;

    public void RegisterPlayer(ulong netId, PlayerController player)
    {
        if (!_spawnedPlayers.ContainsKey(netId)) _spawnedPlayers.Add(netId, player);
    }

    public void UnregisterPlayer(ulong netId)
    {
        if (_spawnedPlayers.ContainsKey(netId)) _spawnedPlayers.Remove(netId);
    }

    // ================= Server Side =================
    // 서버가 매 틱마다 모든 플레이어 위치를 긁어모아서 한 방에 쏨
    private void FixedUpdate()
    {
        if (!IsServer) return;

        // 리스너 없으면 보낼 필요 없음
        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 0)
        {
            return;
        }

        SendBatchUpdate();
    }

    private void SendBatchUpdate()
    {
        // 보낼 데이터 리스트 준비
        var snapshots = new List<PlayerSnapshot>();

        foreach (var kvp in _spawnedPlayers)
        {
            var player = kvp.Value;
            // TODO: 움직임이 있는 애들만 추려서 넣기 (Dirty Check)
            snapshots.Add(new PlayerSnapshot
            (
                kvp.Key,
                player.transform.position,
                player.transform.rotation.eulerAngles.y
            ));
        }

        if (snapshots.Count == 0) return;

        // FastBufferWriter로 직렬화
        // MaxSize는 대충 (사람수 * 구조체크기) 보다 넉넉하게 잡음
        using var writer = new FastBufferWriter(snapshots.Count * 10 + 32, Allocator.Temp);

        writer.WriteValueSafe(snapshots.ToArray()); // 배열 전체를 한 번에 씀

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("BatchMove", writer, NetworkDelivery.UnreliableSequenced);
    }

    // ================= Client Side =================
    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("BatchMove", ReceiveBatchUpdate);
        }
    }

    private void ReceiveBatchUpdate(ulong senderId, FastBufferReader reader)
    {
        // 1. 배열 전체를 한 번에 읽음
        reader.ReadValueSafe(out PlayerSnapshot[] snapshots);

        // 2. 각 플레이어에게 데이터 뿌려주기
        foreach (var snap in snapshots)
        {
            snap.GetState(out ulong netId, out Vector3 pos, out float rotY);

            if (_spawnedPlayers.TryGetValue(netId, out var player))
            {
                // 바로 이동시키지 말고, 목표지점만 설정해줌 (보간은 플레이어가 알아서)
                player.UpdateTargetState(pos, rotY);
            }
        }
    }
}