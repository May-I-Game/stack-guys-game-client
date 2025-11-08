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
    [SerializeField] private string matchApiUrl = "http://54.180.24.20/api/find-game"; // FastAPI 주소
    [SerializeField] private string ticketStatusUrl = "http://54.180.24.20/api/ticket-status"; // 티켓 상태 확인
    private const int MAX_NAME_BYTES = 48;

    private void Start()
    {
#if DUMMY_CLIENT
        if (NetworkManager.Singleton != null)
        {
            //networkManager 콜백 구독
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        if (isLocalMod)
        {
            ConnectToServer();
        }
        else
        {
            StartCoroutine(FindGameAndConnect());
        }
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

    // ========================== FastAPI 매치 요청 (티켓 기반) ==========================
    private IEnumerator FindGameAndConnect()
    {
        isConnecting = true;
        Debug.Log("🎮 Finding game server via FastAPI…");

        // 1단계: 티켓 생성
        using (var req = new UnityWebRequest(matchApiUrl, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes("{}");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 20;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"find-game failed: {req.error}");
                isConnecting = false;
                yield break;
            }

            TicketResponse ticket = null;
            try { ticket = JsonUtility.FromJson<TicketResponse>(req.downloadHandler.text); }
            catch { Debug.LogError("Invalid JSON response from FastAPI"); }

            if (ticket == null || !ticket.success)
            {
                Debug.LogError($"find-game returned invalid: {req.downloadHandler.text}");
                isConnecting = false;
                yield break;
            }

            Debug.Log($"Got ticket: {ticket.ticket_id}");

            // 2단계: 티켓 상태 폴링
            yield return StartCoroutine(PollTicketStatus(ticket.ticket_id, ticket.player_id));
        }
    }

    private IEnumerator PollTicketStatus(string ticketId, string playerId)
    {
        float startTime = Time.time;
        const float maxWaitTime = 60f; // 최대 60초 대기

        while (isConnecting && Time.time - startTime < maxWaitTime)
        {
            string url = $"{ticketStatusUrl}?ticket_id={ticketId}&player_id={playerId}";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"ticket-status failed: {req.error}");
                    yield return new WaitForSeconds(3f);
                    continue;
                }

                TicketStatusResponse status = null;
                try { status = JsonUtility.FromJson<TicketStatusResponse>(req.downloadHandler.text); }
                catch { Debug.LogError("Invalid ticket status JSON"); }

                if (status == null)
                {
                    yield return new WaitForSeconds(3f);
                    continue;
                }

                Debug.Log($"Ticket status: {status.status}");

                if (status.status == "COMPLETED" && status.success)
                {
                    Debug.Log($"Got server info → {status.server_ip}:{status.server_port}");
                    ConnectToServer(status.server_ip, (ushort)status.server_port, status.player_session_id);
                    yield break;
                }
                else if (status.status == "FAILED" || status.status == "CANCELLED" || status.status == "TIMED_OUT")
                {
                    Debug.LogError($"Matchmaking failed: {status.status} - {status.reason}");
                    isConnecting = false;
                    yield break;
                }

                // QUEUED, SEARCHING 등 - 계속 대기 (3초 대기 유지)
                yield return new WaitForSeconds(3f);
            }
        }

        if (isConnecting)
        {
            Debug.LogError("Matchmaking timeout");
            isConnecting = false;
        }
    }

    // ========================== 서버 연결 로직 ==========================
    private void ConnectToServer(string serverAddress, ushort serverPort, string playerSessionId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("❌ NetworkManager not found!");
            isConnecting = false;
            return;
        }

        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("❌ UnityTransport missing on NetworkManager");
            isConnecting = false;
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        transport.UseWebSockets = true;  // WebGL 강제
#endif
        transport.SetConnectionData(serverAddress, serverPort);
        Debug.Log($"Connecting to {serverAddress}:{serverPort} ...");

        // ConnectionData 구성: [1바이트 캐릭터][이름(UTF8 ≤16B)][0x00][playerSessionId UTF8]
        byte[] nameBytes = TruncateUtf8(clientName, MAX_NAME_BYTES);
        byte[] sessionBytes = System.Text.Encoding.UTF8.GetBytes(playerSessionId ?? "");
        byte[] payload = new byte[1 + nameBytes.Length + 1 + sessionBytes.Length];
        payload[0] = (byte)clientCharIndex;
        System.Array.Copy(nameBytes, 0, payload, 1, nameBytes.Length);
        payload[1 + nameBytes.Length] = 0;
        System.Array.Copy(sessionBytes, 0, payload, 1 + nameBytes.Length + 1, sessionBytes.Length);

        nm.NetworkConfig.ConnectionData = payload;
        PlayerPrefs.SetString("player_name", clientName);
        PlayerPrefs.Save();

        if (!nm.StartClient())
        {
            Debug.LogError("❌ StartClient failed");
            isConnecting = false;
            return;
        }

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
