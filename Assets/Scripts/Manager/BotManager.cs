using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BotManager : NetworkBehaviour
{
    [Header("Bot Prefab")]
    [SerializeField] private GameObject botPrefab;

    [SerializeField] private Transform[] spawnPoints;


}
