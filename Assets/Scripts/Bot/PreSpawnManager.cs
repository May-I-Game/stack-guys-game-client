using UnityEngine;
using Unity.Netcode;

public class PreSpawnManager : MonoBehaviour
{
    [Header("Pre-Spawn Point Settings")]
    [SerializeField] private bool autoCollectChildPoints = true;
    [SerializeField] private Transform[] preSpawnPoints;

    private void Awake()
    {
        // 자동 수집 옵션이 켜져있으면 자식들 수집
        if (autoCollectChildPoints)
        {
            CollectSpawnPoints();
        }
    }

    // 자식 Transform들을 배열로 수집
    public void CollectSpawnPoints()
    {
        System.Collections.Generic.List<Transform> points = new System.Collections.Generic.List<Transform>();

        foreach (Transform child in transform)
        {
            points.Add(child);
        }

        preSpawnPoints = points.ToArray();
    }

    // Pre-SpawnPoint 배열 반환
    public Transform[] GetPreSpawnPoints()
    {
        return preSpawnPoints;
    }
}
