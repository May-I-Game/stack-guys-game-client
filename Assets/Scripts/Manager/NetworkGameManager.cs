using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager instance;
    private NetworkManager networkManager;

    [Header("Game Settings")]
    [SerializeField] private bool isServerMod;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("Background Handling")]
    [SerializeField] private float maxBackgroundTime = 30f;

    private bool hasInitialized = false;
    private Dictionary<ulong, int> clientCharacterSelections = new Dictionary<ulong, int>();
    private Dictionary<ulong, string> clientPlayerNames = new Dictionary<ulong, string>();

    // 백그라운드 처리 변수
    private bool isInBackground = false;
    private float backgroundStartTime = 0f;

    public bool isObserver { get; private set; } = false;

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
        if (isServerMod)
        {
            StartServerAndLoadScene();
        }
        else
        {
            Debug.Log("--- SERVER BUILD CLIENT MOD DETECTED ---");
        }
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

        // 이벤트 구독
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        //connectionApproval 설정
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
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

    private void StartServerAndLoadScene()
    {
        Debug.Log("--- SERVER BUILD DETECTED ---");
        Debug.Log("-----  SERVER START  -----");

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
                // Debug.Log($"[Server] Set PlayerName NetworkVariable to '{playerName}' for client {clientId}");
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
            // 옵저버 모드
            if (!clientPlayerNames.ContainsKey(clientId))
            {
                Debug.Log($"[Server Log] Observer connected. Client ID: {clientId}");
                Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");
            }

            else
            {
                Debug.Log($"[Server Log] Client connecting... Client ID: {clientId}");
                Debug.Log($"[Server Log] Name: {clientPlayerNames[clientId]}, Character: {clientCharacterSelections[clientId]}");
                Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

                //플레이어 스폰
                SpawnPlayerForClient(clientId);
            }
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

            // dictionary 정리
            if (clientCharacterSelections.ContainsKey(clientId))
            {
                clientCharacterSelections.Remove(clientId);
            }
        }

        // 클라이언트의 경우만 Login으로 이동
        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Disconnected from server");
            SceneManager.LoadScene("Login");
        }
    }

    //ConnectionApproval 콜백
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int characterIndex = 0;
        string playerName = null;

        if (isObserver)
        {
            Debug.Log($"[Server] Observer connection approved: {request.ClientNetworkId}");
        }

        else if (request.Payload != null && request.Payload.Length > 0)
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

            //서버 dictionary에 저장
            clientCharacterSelections[request.ClientNetworkId] = characterIndex;
            clientPlayerNames[request.ClientNetworkId] = playerName;
        }

        //연결 승인
        response.Approved = true;
        response.CreatePlayerObject = false;
    }

    public void SetObserverMode()
    {
        isObserver = true;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // 백그라운드로 전환
            OnEnterBackground();
        }
        else
        {
            // 포그라운드로 복귀
            OnReturnForeground();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && !isInBackground)
        {
            // 포커스 잃음 (Alt+Tab 등)
            OnEnterBackground();
        }
        else if (hasFocus && isInBackground)
        {
            // 포커스 복귀
            OnReturnForeground();
        }
    }

    private void OnEnterBackground()
    {
        if (isInBackground) return;

        isInBackground = true;
        backgroundStartTime = Time.realtimeSinceStartup;

        Debug.Log("[NetworkGameManager] 백그라운드 전환 감지");

        if (networkManager == null) return;

        // TODO: 서버에 패킷전송 중지요청

        // Pause 모드
        Debug.Log("[NetworkGameManager] 백그라운드 일시정지 모드 (연결 유지)");
        PausePlayer();
    }

    private void OnReturnForeground()
    {
        if (!isInBackground) return;

        float backgroundDuration = Time.realtimeSinceStartup - backgroundStartTime;
        isInBackground = false;

        Debug.Log($"[NetworkGameManager] 포그라운드 복귀 (백그라운드 시간: {backgroundDuration:F1}초)");

        if (networkManager == null) return;

        // TODO: 서버에 패킷전송 재개요청

        // 너무 오래 백그라운드에 있었으면 연결이 끊겼을 수 있음
        if (backgroundDuration > maxBackgroundTime)
        {
            Debug.LogWarning("[NetworkGameManager] 백그라운드 시간 초과 - 연결 확인 필요");

            if (!networkManager.IsConnectedClient)
            {
                Debug.LogError("[NetworkGameManager] 서버 연결 끊김 - 재연결 필요");
                OnConnectionLost();
                return;
            }
        }

        // Pause 모드: 플레이어 제어 복원
        Debug.Log("[NetworkGameManager] 포그라운드 복귀 - 플레이어 제어 재개");
        ResumePlayer();
    }

    private void PausePlayer()
    {
        // 로컬 플레이어 찾기
        if (networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null)
        {
            GameObject playerObject = networkManager.LocalClient.PlayerObject.gameObject;
            PlayerController controller = playerObject.GetComponent<PlayerController>();

            if (controller != null)
            {
                // 입력 차단
                controller.SetInputEnabled(false);
                Debug.Log("[NetworkGameManager] 플레이어 입력 차단");
            }
        }
    }

    private void ResumePlayer()
    {
        // 로컬 플레이어 찾기
        if (networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null)
        {
            GameObject playerObject = networkManager.LocalClient.PlayerObject.gameObject;
            PlayerController controller = playerObject.GetComponent<PlayerController>();

            if (controller != null)
            {
                // 입력 재개
                controller.SetInputEnabled(true);
                Debug.Log("[NetworkGameManager] 플레이어 입력 재개");
            }
        }
    }

    private void OnConnectionLost()
    {
        // 연결 끊김 처리
        Debug.LogWarning("[NetworkGameManager] 서버 연결 끊김 감지");

        // TODO: UI 표시
        // - "서버 연결이 끊어졌습니다" 메시지
        // - 재연결 버튼
        // - 메인 메뉴로 가기 버튼
    }
}