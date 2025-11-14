using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

public class DummyGameStarter : MonoBehaviour
{
    private int clientCharIndex = 12;
    private string clientName = "BotClient";
    private bool isConnecting;

    [SerializeField] private bool isLocalMod;
    [SerializeField] private string serverAddress = "3.37.88.2";
    [SerializeField] private ushort serverPort = 7779;
    private const int MAX_NAME_BYTES = 48;

    private void Start()
    {
#if DUMMY_CLIENT
        StartCoroutine(WaitAndConnect());
#endif
    }

    private IEnumerator WaitAndConnect()
    {
        // NetworkManager가 초기화될 때까지 대기
        while (NetworkManager.Singleton == null)
        {
            Debug.Log("[DummyGameStarter] Waiting for NetworkManager...");
            yield return new WaitForSeconds(0.1f);
        }

        // NetworkManager가 이미 시작되었는지 확인
        while (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[DummyGameStarter] Waiting for NetworkManager to be ready...");
            yield return new WaitForSeconds(0.1f);
        }

        //networkManager 콜백 구독
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log("[DummyGameStarter] NetworkManager ready, starting connection...");

        // 추가 안전성을 위해 약간 더 대기
        yield return new WaitForSeconds(0.5f);

        ConnectToServer();
    }

    private void OnDestroy()
    {
#if DUMMY_CLIENT
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        CancelInvoke(nameof(CheckConnectionTimeout));
#endif
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[DummyGameStarter] OnClientConnected called - ClientId: {clientId}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");

        //성공적으로 자신이 연결됨
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[DummyGameStarter] Successfully connected to server!");
            CancelInvoke(nameof(CheckConnectionTimeout));
            isConnecting = false;
        }
        else
        {
            Debug.Log($"[DummyGameStarter] Other client connected: {clientId}");
        }
    }
    private void OnClientDisconnected(ulong clientId)
    {
        //자신이 연결 해제됨
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Disconnected from server");
            if (isConnecting)
            {
                Debug.Log("Connected failed");
                isConnecting = false;
            }
        }
    }

    private void ConnectToServer()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager not found!");
            isConnecting = false;
            return;
        }

        isConnecting = true;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport not found!");
            isConnecting = false;
            return;
        }

        Debug.Log($"[DummyGameStarter] Attempting to connect to {serverAddress}:{serverPort}");

        transport.SetConnectionData(serverAddress, serverPort);

        // ConnectionData 구성: [1바이트 캐릭터][이름(UTF8)]
        byte[] nameBytes = TruncateUtf8(clientName, MAX_NAME_BYTES);
        byte[] payload = new byte[1 + nameBytes.Length];
        payload[0] = (byte)clientCharIndex;
        System.Array.Copy(nameBytes, 0, payload, 1, nameBytes.Length);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;

        //서버로 캐릭터 이름을 보내기
        PlayerPrefs.SetString("player_name", clientName);
        PlayerPrefs.Save();

        Debug.Log($"[DummyGameStarter] Character Index: {clientCharIndex}, Name: {clientName}");
        Debug.Log($"[DummyGameStarter] Connection payload size: {payload.Length} bytes");

        //클라이언트 시작
        bool startResult = NetworkManager.Singleton.StartClient();

        //클라-서버 연결 실패했을 경우
        if (!startResult)
        {
            Debug.LogError("[DummyGameStarter] StartClient() returned false - 연결 실패");
            isConnecting = false;
            return;
        }

        Debug.Log("[DummyGameStarter] StartClient() succeeded, waiting for connection...");
        Invoke(nameof(CheckConnectionTimeout), 10f);
    }

    // UTF-8 바이트 안전 자르기
    private static byte[] TruncateUtf8(string s, int maxBytes)
    {
        var src = System.Text.Encoding.UTF8.GetBytes(s ?? "");
        if (src.Length <= maxBytes) return src;
        int len = maxBytes;
        while (len > 0 && (src[len] & 0b1100_0000) == 0b1000_0000) len--;
        var dst = new byte[len];
        System.Array.Copy(src, dst, len);
        return dst;
    }

    private void CheckConnectionTimeout()
    {
        if (isConnecting && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("Connection timeout!");
            if (NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }
            isConnecting = false;
        }
    }
}
