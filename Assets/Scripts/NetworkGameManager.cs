using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [Header("Server Settings")]
    [SerializeField] private ushort serverPort = 7779;

    [Header("Client Settings")]
    [SerializeField] private string serverAddress = "127.0.0.1";

    private NetworkManager networkManager;
    private UnityTransport transport;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Initialize()
    {
        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();

        //이벤트 구독
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;
    }

    #region Client Methods
    public void StartClient(string address, ushort port)
    {
        serverAddress = address;
        serverPort = port;

        //Transport 설정
        transport.SetConnectionData(serverAddress, serverPort);

        //클라이언트 시작
        bool success = networkManager.StartClient();

        if (success)
        {
            Debug.Log($"Connecting to server at {serverAddress}:{serverPort}...");
        }
        else
        {
            Debug.LogError("Failed to start client");
        }
    }
    private void OnClientConnected(ulong clientId)
    {
        //로컬 클라이언트가 연결되었을 때
        if (clientId == networkManager.LocalClientId)
        {
            Debug.Log("Successfully connected to server!");

            //Game 씬으로 이동
            if (SceneManager.GetActiveScene().name == "Login")
            {
                SceneManager.LoadScene("GameScene");
            }
        }
    }
    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == networkManager.LocalClientId)
        {
            Debug.Log("Disconnected from server");

            // Login 씬으로 돌아가기
            if (SceneManager.GetActiveScene().name == "GameScene")
            {
                SceneManager.LoadScene("Login");
            }
        }
    }
    public void Disconnect()
    {
        if (networkManager.IsClient)
        {
            networkManager.Shutdown();
        }
    }
    #endregion

    #region Server Methods

    public void StartServer()
    {
        //transport 설정
        transport.SetConnectionData("0.0.0.0", serverPort);

        //서버 시작
        bool success = networkManager.StartServer();

        if (success)
        {
            Debug.Log($"Server started on port {serverPort}");
        }
        else
        {
            Debug.LogError("Failed to start server!");
        }
    }
    private void OnServerStarted()
    {
        Debug.Log("Server is ready for connections");

        //서버는 자동으로 Game 씬 로드
        if (SceneManager.GetActiveScene().name != "GameScene")
        {
            SceneManager.LoadScene("GameScene");
        }
    }
    #endregion

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }
}
