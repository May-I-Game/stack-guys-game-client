using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkGameManager : MonoBehaviour
{
    private NetworkManager networkManager;
    [SerializeField]
    private string gameSceneName = "GameScene";

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
    }

    private void OnClientConnected(ulong clientId)
    {
        // 서버 로그
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client connecting... Client ID: {clientId}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");
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

    private void OnClientDisconnected(ulong clientId)
    {
        // 서버 로그
        if (networkManager.IsServer)
        {
            Debug.Log($"[Server Log] Client disconnected. Client ID: {clientId}");
            Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");
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
