using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkGameManager : MonoBehaviour
{
    private static NetworkGameManager instance;
    private NetworkManager networkManager;

    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private GameObject[] characterPrefabs;

    private bool hasInitialized = false;
    private Dictionary<ulong, int> clientCharacterSelections = new Dictionary<ulong, int>();
    private Dictionary<ulong, string> clientPlayerNames = new Dictionary<ulong, string>();
    private void Awake()
    {
        // 싱글톤 패턴으로 중복 방지
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 한 번만 초기화
        if (!hasInitialized)
        {
            Initialize();
            hasInitialized = true;
        }

#if UNITY_SERVER
        StartServerAndLoadScene();
#elif DUMMY_CLIENT
        Debug.Log("--- BOT CLIENT BUILD DETECTED ---");
#else
        Debug.Log("--- CLIENT BUILD DETECTED ---");
#endif
    }

    private void Initialize()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("NetworkManger.Singleton is null!");
            return;
        }
        // 이벤트 구독 전에 기존 구독 해제 (중복 방지)
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;

        // 새로 구독
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        //connectionApproval 설정
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
    }

    private void StartServerAndLoadScene()
    {
        Debug.Log("--- SERVER BUILD DETECTED (Batch Mode) ---");
        Debug.Log("     --------  SERVER START  --------     ");

        NetworkManager.Singleton.StartServer();

        // 현재 씬이 이미 GameScene이 아닐 때만 로드
        if (SceneManager.GetActiveScene().name != gameSceneName)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }


    private void SpawnPlayerForClient(ulong clientId)
    {
        //Dictionary에서 데이터 가져오기
        int characterIndex = clientCharacterSelections.ContainsKey(clientId) ? clientCharacterSelections[clientId] : 0;

        string playerName = clientPlayerNames.ContainsKey(clientId) ? clientPlayerNames[clientId] : $"Player_{clientId}";

        //캐릭터 프리팹 선택
        GameObject playerPrefab = characterPrefabs[characterIndex];
        Vector3 spawnPosition = GetSpawnPosition(clientId);

        // 캐릭터 인스턴스 생성
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            //네트워크 오브젝트로 스폰
            networkObject.SpawnAsPlayerObject(clientId, true);

            // 이름을 UI text에 설정(스폰 직후)
            PlayerNameSync nameSync = playerInstance.GetComponent<PlayerNameSync>();
            if (nameSync != null)
            {
                nameSync.SetPlayerName(playerName);
                Debug.Log($"[Server] Set PlayerName NetworkVariable to '{playerName}' for client {clientId}");
            }

            Debug.Log($"[Server] Spawned character {characterIndex} with name '{playerName}' for client {clientId}");
        }
        else
        {
            Debug.LogError($"NetworkObject component missing on player prefab!");
        }
    }

    private Vector3 GetSpawnPosition(ulong clientId)
    {
        //서버가 클라이언트의 캐릭터를 생성시킬 좌표를 반환
        return new Vector3(0, 1, 0);
    }
    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client connecting... Client ID: {clientId}");
            Debug.Log($"[Server Log] Name: {clientPlayerNames[clientId]}, Character: {clientCharacterSelections[clientId]}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

            //플레이어 스폰
            SpawnPlayerForClient(clientId);
        }

        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Successfully connected to server!");
        }
    }
    private void OnClientDisconnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client disconnected. Client ID: {clientId}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

            //dictionary 정리
            if (clientCharacterSelections.ContainsKey(clientId))
            {
                clientCharacterSelections.Remove(clientId);
            }
        }

        // 클라이언트의 경우만 Login으로 이동
        if (networkManager.IsClient && !networkManager.IsServer && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Disconnected from server");
        }
    }
    private void OnDestroy()
    {
        // instance가 자신일 때만 정리
        if (instance == this)
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            instance = null;
        }
    }

    //ConnectionApproval 콜백
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int characterIndex = 0;
        string playerName = null;

        if (request.Payload != null && request.Payload.Length > 0)
        {
            //캐릭터 인덱스 받아오기
            characterIndex = request.Payload[0];

            //유효성 검사
            if (characterIndex < 0 || characterIndex >= characterPrefabs.Length)
            {
                Debug.LogWarning($"Invalid character Index {characterIndex}, using default 0");
                characterIndex = 0;
            }
            // 캐릭터 이름
            if (request.Payload.Length > 1)
            {
                playerName = System.Text.Encoding.UTF8.GetString(request.Payload, 1, request.Payload.Length - 1);
                playerName = playerName.Trim('\0');

                // 빈 문자열 처리(문자열이 비면 안되긴 함)
                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = $"Player_{request.ClientNetworkId}";
                }
            }
            Debug.Log($"[Server] Client {request.ClientNetworkId}: Character={characterIndex}, Name={playerName}");
        }
        //서버 dictionary에 저장
        clientCharacterSelections[request.ClientNetworkId] = characterIndex;
        clientPlayerNames[request.ClientNetworkId] = playerName;
        //연결 승인
        response.Approved = true;
        response.CreatePlayerObject = false;
    }
}