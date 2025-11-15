using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPoolManager : NetworkBehaviour, INetworkPrefabInstanceHandler
{
    [Header("Settings")]
    public NetworkObject PrefabToPool;
    public int InitialPoolSize = 500;

    private Queue<NetworkObject> pool = new Queue<NetworkObject>();

    private void Awake()
    {
        pool = new Queue<NetworkObject>();
    }

    public override void OnNetworkSpawn()
    {
        // 핸들러 등록 (서버/클라 모두 등록은 해야 함)
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.PrefabHandler.AddHandler(PrefabToPool, this);
        }

        // 서버인 경우 미리 500개 만들어두기
        if (IsServer)
        {
            for (int i = 0; i < InitialPoolSize; i++)
            {
                CreateNewInstance();
            }
            Debug.Log($"[NetworkPoolManager] {InitialPoolSize}개 시체 풀링 완료");
        }
    }

    // 핸들러 해제
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.PrefabHandler.RemoveHandler(PrefabToPool);
        }
    }

    private NetworkObject CreateNewInstance()
    {
        NetworkObject obj = Instantiate(PrefabToPool);
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
        return obj;
    }

    // 넷코드가 객체 Spawn시에 호출
    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        // [서버] : 풀에서 꺼내 씀
        if (IsServer)
        {
            NetworkObject netObj;
            if (pool.Count > 0)
            {
                netObj = pool.Dequeue();
            }
            else
            {
                netObj = CreateNewInstance();
                netObj = pool.Dequeue();
            }

            netObj.transform.position = position;
            netObj.transform.rotation = rotation;
            netObj.gameObject.SetActive(true);

            // Debug.Log($"[NetworkPoolManager] 시체 1개 풀에서 사용 완료!");
            // Debug.Log($"[NetworkPoolManager] 풀에 {pool.Count}개 시체 남음!");

            return netObj;
        }

        // [클라이언트] : 그냥 쌩으로 생성 (풀링 안 함)
        else
        {
            // 클라는 알아서 만들어서 씀
            NetworkObject netObj = Instantiate(PrefabToPool, position, rotation);
            return netObj;
        }
    }

    // 넷코드가 객체 DeSpawn시에 호출
    public void Destroy(NetworkObject networkObject)
    {
        // [서버] : 다시 풀에 반납
        if (IsServer)
        {
            networkObject.gameObject.SetActive(false);
            pool.Enqueue(networkObject);
        }

        // [클라이언트] : 진짜 파괴 (메모리 해제)
        else
        {
            Destroy(networkObject.gameObject);
        }
    }
}
