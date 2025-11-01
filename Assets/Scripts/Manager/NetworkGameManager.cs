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
    
    private Dictionary<ulong, int> clientCharacterSelections = new Dictionary<ulong, int>();
    private bool hasInitialized = false;
    private bool isLoadingScene = false;

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

        // 배치 모드에서 실행 시 자동으로 서버 시작
        if (Application.isBatchMode)
        {
            StartServerAndLoadScene();
        }
        else
        {
            Debug.Log("--- CLIENT BUILD DETECTED ---");
        }
    }

    private void Initialize()
    {
        networkManager = NetworkManager.Singleton;
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager.Singleton is null!");
            return;
        }

        // 이벤트 구독 전에 기존 구독 해제 (중복 방지)
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        networkManager.ConnectionApprovalCallback -= ApprovalCheck;

        // 새로 구독
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // ConnectionApproval 설정
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
    }

    private void StartServerAndLoadScene()
    {
        if (isLoadingScene) return; // 중복 로딩 방지
        
        Debug.Log("--- SERVER BUILD DETECTED (Batch Mode) ---");
        Debug.Log("     --------  SERVER START  --------     ");

        NetworkManager.Singleton.StartServer();
        
        // 현재 씬이 이미 GameScene이 아닐 때만 로드
        if (SceneManager.GetActiveScene().name != gameSceneName)
        {
            isLoadingScene = true;
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int selectedCharacterIndex = 0;

        if (request.Payload != null && request.Payload.Length > 0)
        {
            selectedCharacterIndex = request.Payload[0];

            if (selectedCharacterIndex < 0 || selectedCharacterIndex >= characterPrefabs.Length)
            {
                Debug.LogWarning($"Invalid character index {selectedCharacterIndex}, using default 0");
                selectedCharacterIndex = 0;
            }
        }

        clientCharacterSelections[request.ClientNetworkId] = selectedCharacterIndex;

        response.Approved = true;
        response.CreatePlayerObject = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client connecting... Client ID: {clientId}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

            SpawnPlayerForClient(clientId);
        }

        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Successfully connected to server!");
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        int characterIndex = 0;
        if (clientCharacterSelections.ContainsKey(clientId))
        {
            characterIndex = clientCharacterSelections[clientId];
        }

        GameObject playerPrefab = characterPrefabs[characterIndex];
        Vector3 spawnPosition = GetSpawnPosition(clientId);

        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId, true);
        }
        else
        {
            Debug.LogError($"NetworkObject component missing on player prefab!");
        }
    }

    private Vector3 GetSpawnPosition(ulong clientId)
    {
        return new Vector3(0, 1, 0);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client disconnected. Client ID: {clientId}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

            if (clientCharacterSelections.ContainsKey(clientId))
            {
                clientCharacterSelections.Remove(clientId);
            }
        }

        // 클라이언트의 경우만 Login으로 이동
        if (networkManager.IsClient && !networkManager.IsServer && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Disconnected from server");
            
            // 약간의 딜레이 후 씬 전환 (스택 정리 시간 확보)
            Invoke(nameof(ReturnToLogin), 0.5f);
        }
    }

    private void ReturnToLogin()
    {
        if (!isLoadingScene)
        {
            isLoadingScene = true;
            SceneManager.LoadScene("Login");
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
                networkManager.ConnectionApprovalCallback -= ApprovalCheck;
            }
            instance = null;
        }
    }
}