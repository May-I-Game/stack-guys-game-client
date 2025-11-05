using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class DummyGameStarter : MonoBehaviour
{
    private int clientCharIndex = 12;
    private string clientName = "BotClient";
    private bool isConnecting;

    private void Start()
    {
#if DUMMY_CLIENT
        if (NetworkManager.Singleton != null)
        {
            //networkManager 콜백 구독
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        ConnectToServer();
#endif
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
        //성공적으로 자신이 연결됨
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Successfully connected to server!");
            CancelInvoke(nameof(CheckConnectionTimeout));
            isConnecting = false;
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
            Debug.Log("NetworkManager not found!");
            isConnecting = false;
            return;
        }

        isConnecting = true;

        //서버로 캐릭터 인덱스를 보내기
        byte[] payload = new byte[17];

        payload[0] = (byte)clientCharIndex;
        // 이름을 ASCII 바이트로 변환
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(clientName);

        // 이름 복사 (최대 16바이트)
        int bytesToCopy = Mathf.Min(nameBytes.Length, 16);
        System.Array.Copy(nameBytes, 0, payload, 1, bytesToCopy);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;

        //서버로 캐릭터 이름을 보내기
        PlayerPrefs.SetString("player_name", clientName);
        PlayerPrefs.Save();

        Debug.Log($"Character Index : {clientCharIndex}, Name: {clientName}");

        //클라이언트 시작
        bool startResult = NetworkManager.Singleton.StartClient();

        //클라-서버 연결 실패했을 경우
        if (!startResult)
        {
            Debug.Log("연결 실패");
            isConnecting = false;
            return;
        }

        Invoke(nameof(CheckConnectionTimeout), 10f);
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
