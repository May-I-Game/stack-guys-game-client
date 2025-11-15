using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct PlayerSnapshot : INetworkSerializeByMemcpy
{
    public ushort NetworkObjectId; // 누가 주인인가? (2 Byte)
    public short X, Y, Z;         // 위치 (6 Byte)
    public ushort YRotation;       // Y회전 (2 Byte) 

    // 100f : 1cm 단위 정밀도 (-327.68 ~ +327.67m)
    // 50f  : 2cm 단위 정밀도 (-655.36 ~ +655.34m)
    // 20f  : 5cm 단위 정밀도 (-1638.4 ~ +1638.35m)
    private const float COMPRESS_RATIO = 50f;

    // 생성자로 데이터를 넣을 때 자동 압축
    public PlayerSnapshot(ulong netId, Vector3 pos, float rotY)
    {
        NetworkObjectId = (ushort)netId; // 주의: ID 65535 넘으면 오버플로우

        X = (short)Mathf.RoundToInt(pos.x * COMPRESS_RATIO);
        Y = (short)Mathf.RoundToInt(pos.y * COMPRESS_RATIO);
        Z = (short)Mathf.RoundToInt(pos.z * COMPRESS_RATIO);

        float normalizedRot = Mathf.Repeat(rotY, 360f);
        YRotation = (ushort)(normalizedRot / 360f * 65535f); // 0.005도 오차
    }

    // 데이터를 꺼낼 때
    public void GetState(out ulong netId, out Vector3 pos, out float rotY)
    {
        netId = (ulong)NetworkObjectId;

        pos = new Vector3(
            X / COMPRESS_RATIO,
            Y / COMPRESS_RATIO,
            Z / COMPRESS_RATIO
        );

        rotY = (float)YRotation / 65535f * 360f;
    }
}

public class BatchNetworkManager : NetworkBehaviour
{
    [SerializeField]
    private float syncDistance = 30f;
    private float _sqrSyncDistance;

    // 빠른 검색을 위해 로컬 플레이어들을 캐싱해둠
    private Dictionary<ulong, PlayerController> _spawnedPlayers = new Dictionary<ulong, PlayerController>();
    // [최적화] 재사용할 리스트 (GC 방지) - 미리 넉넉하게 할당
    private List<PlayerSnapshot> _snapshotBuffer = new List<PlayerSnapshot>(200);

    public static BatchNetworkManager Instance;

    private void Awake()
    {
        Instance = this;
        _sqrSyncDistance = syncDistance * syncDistance;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.NetworkTickSystem.Tick += OnNetworkTick;
        }

        if (IsClient)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("BatchMove", ReceiveBatchUpdate);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.NetworkTickSystem.Tick -= OnNetworkTick;
        }

        if (IsClient)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("BatchMove");
        }
    }

    public void RegisterPlayer(ulong netId, PlayerController player)
    {
        if (!_spawnedPlayers.ContainsKey(netId)) _spawnedPlayers.Add(netId, player);
    }

    public void UnregisterPlayer(ulong netId)
    {
        if (_spawnedPlayers.ContainsKey(netId)) _spawnedPlayers.Remove(netId);
    }

    // ================= Server Side =================
    // 서버가 매 틱마다 모든 플레이어 위치를 한번에 전송
    private void OnNetworkTick()
    {
        // 리스너 없으면 보낼 필요 없음
        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 0) return;

        SendBatchUpdate();
    }

    private void SendBatchUpdate()
    {
        // 클라이언트별로 개별 전송 (각자 시야에 보이는 것만)
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            var observer = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            if (observer == null) continue;

            // 스냅샷 버퍼 초기화
            _snapshotBuffer.Clear();
            foreach (var kvp in _spawnedPlayers)
            {
                PlayerController other = kvp.Value;

                // 관심영역 체크 (거리 기반)
                float sqrDistance = (observer.transform.position - other.transform.position).sqrMagnitude;
                if (sqrDistance > _sqrSyncDistance) continue;

                // TODO: Dirty Check (움직임 있는 것만)

                // TODO: 델타 컴프레션?

                // other을 스냅샷에 추가해서 동기화
                _snapshotBuffer.Add(new PlayerSnapshot(
                    kvp.Key,
                    other.transform.position,
                    other.transform.rotation.eulerAngles.y
                ));
            }

            if (_snapshotBuffer.Count == 0) continue;

            // 스냅샷 전송
            SendSnapshots(clientId, _snapshotBuffer);
        }
    }

    private void SendSnapshots(ulong clientId, List<PlayerSnapshot> snapshots)
    {
        using var writer = new FastBufferWriter(snapshots.Count * 10 + 64, Allocator.Temp);
        writer.WriteValueSafe(snapshots.ToArray());

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            "BatchMove",
            clientId, // 특정 클라이언트에게만 전송
            writer,
            NetworkDelivery.UnreliableSequenced
        );

        Debug.Log($"[BatchNetworkManager] 전송: {snapshots.Count}명, 크기: {snapshots.Count * 10}바이트");

        #region Chunked Sending (Not Used)
        //const int CHUNK_SIZE = 20;

        //for (int i = 0; i < snapshots.Count; i += CHUNK_SIZE)
        //{
        //    int count = Mathf.Min(CHUNK_SIZE, snapshots.Count - i);
        //    var chunk = snapshots.GetRange(i, count).ToArray();

        //    int bufferSize = count * 20 + 256;
        //    using var writer = new FastBufferWriter(bufferSize, Allocator.Temp);

        //    writer.WriteValueSafe(chunk);

        //    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
        //        "BatchMove",
        //        clientId, // 특정 클라이언트에게만 전송
        //        writer,
        //        NetworkDelivery.UnreliableSequenced
        //    );
        //}
        #endregion
    }

    // ================= Client Side =================
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