using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkGameManager : MonoBehaviour
{
    private NetworkManager networkManager;
    [SerializeField]
    private string gameSceneName = "GameScene";

    // 캐릭터 프리팹 배열 (Inspector에서 할당)
    [SerializeField] private GameObject[] characterPrefabs;

    // 클라이언트별 선택한 캐릭터 인덱스 저장
    private Dictionary<ulong, int> clientCharacterSelections = new Dictionary<ulong, int>();

    private void Start()
    {
        Initialize();

        // 배치 모드에서 실행 시 자동으로 서버 시작
        if (Application.isBatchMode)
        {
            Debug.Log("--- SERVER BUILD DETECTED (Batch Mode) ---");
            Debug.Log("     --------  SERVER START  --------     ");

            NetworkManager.Singleton.StartServer();
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        else
        {
            Debug.Log("--- CLIENT BUILD DETECTED ---");
        }
    }

    private void Initialize()
    {
        networkManager = NetworkManager.Singleton;

        //이벤트 구독
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // ConnectionApproval 활성화 및 콜백 추가
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
    }

    // 연결 승인 및 캐릭터 인덱스 수신
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // 기본값: 캐릭터 인덱스 0 (첫 번째 캐릭터)
        int selectedCharacterIndex = 0;

        // 클라이언트가 보낸 데이터 파싱
        if (request.Payload != null && request.Payload.Length > 0)
        {
            selectedCharacterIndex = request.Payload[0]; // 첫 번째 바이트가 캐릭터 인덱스

            // 유효성 검사
            if (selectedCharacterIndex < 0 || selectedCharacterIndex >= characterPrefabs.Length)
            {
                Debug.LogWarning($"Invalid character index {selectedCharacterIndex}, using default 0");
                selectedCharacterIndex = 0;
            }
        }

        // 클라이언트 ID와 캐릭터 인덱스 매핑 저장
        clientCharacterSelections[request.ClientNetworkId] = selectedCharacterIndex;

        // 연결 승인
        response.Approved = true;
        response.CreatePlayerObject = false; // 수동으로 스폰할 것이므로 false
    }

    private void OnClientConnected(ulong clientId)
    {
        // 서버 로그
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client connecting... Client ID: {clientId}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

            // 서버인 경우 플레이어 스폰
            SpawnPlayerForClient(clientId);
        }

        // 클라이언트 로그
        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Successfully connected to server!");

            // 필요없는 로직 서버에 연결되면 "서버가 있는 씬" 으로 NetworkManager가 자동으로 로드시킴
            //Game 씬으로 이동
            //if (SceneManager.GetActiveScene().name == "Login")
            //{
            //    SceneManager.LoadScene("GameScene");
            //}
        }
    }

    // 클라이언트별 캐릭터 스폰
    private void SpawnPlayerForClient(ulong clientId)
    {
        // 저장된 캐릭터 인덱스 가져오기 (없으면 0)
        int characterIndex = 0;
        if (clientCharacterSelections.ContainsKey(clientId))
        {
            characterIndex = clientCharacterSelections[clientId];
        }

        // 선택한 캐릭터 프리팹으로 플레이어 생성
        GameObject playerPrefab = characterPrefabs[characterIndex];

        // 스폰 위치 계산
        Vector3 spawnPosition = GetSpawnPosition(clientId);

        // 플레이어 인스턴스 생성
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

        // NetworkObject로 스폰 (클라이언트에게 오너십 부여)
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        networkObject.SpawnAsPlayerObject(clientId, true);
    }

    // 스폰 위치 계산
    private Vector3 GetSpawnPosition(ulong clientId)
    {
        // 기본 스폰 위치
        return new Vector3(0, 1, 0);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // 서버 로그
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client disconnected. Client ID: {clientId}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

            // 클라이언트 연결 해제 시 저장된 선택 정보 제거
            if (clientCharacterSelections.ContainsKey(clientId))
            {
                clientCharacterSelections.Remove(clientId);
            }
        }

        // 클라이언트 로그
        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Disconnected from server");

            // 어디에 있던 Login 씬으로 돌아가기
            SceneManager.LoadScene("Login");
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
