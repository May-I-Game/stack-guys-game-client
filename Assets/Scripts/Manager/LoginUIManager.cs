using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// LoginUIManager
///  - FastAPI를 통해 GameLift 매치메이킹 요청 (티켓 기반)
///  - 서버 IP/Port/PlayerSessionId 수신 후 UnityTransport로 연결
///  - WebGL에서는 WebSocket 모드로 전환
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    // 에디터에서 접속할 주소
    [SerializeField] public string serverAddress = "127.0.0.1";
    [SerializeField] ushort serverPort = 7779;

    [SerializeField] private string matchApiUrl = "http://54.180.24.20/api/find-game";   // FastAPI 주소
    [SerializeField] private string ticketStatusUrl = "http://54.180.24.20/api/ticket-status"; // 티켓 상태 확인
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Camera characterSelectCamera;
    [SerializeField] private GameObject characterSelectPopup;

    [Header("Loading UI")]
    [Tooltip("로딩 중 표시할 UI 패널 (캔버스에 미리 배치되어 있어야 함)")]
    public GameObject loadingPanel;

    private int clientCharIndex;
    private string clientName;
    private bool isConnecting = false;
    private AudioSource audioSource;

    private const int MAX_NAME_BYTES = 48;

    void Start()
    {
        characterSelectPopup?.SetActive(false);
        audioSource = GetComponent<AudioSource>();
        loadingPanel.SetActive(false);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        CancelInvoke(nameof(CheckConnectionTimeout));
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("✅ Successfully connected to server!");
            CancelInvoke(nameof(CheckConnectionTimeout));
            isConnecting = false;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Disconnected from server");
            if (isConnecting)
            {
                Debug.Log("❌ Connection failed");
                isConnecting = false;
                OnConnectionFailed("Client connection attempt failed.");
            }
        }
    }

    // ========================== 캐릭터 선택 ==========================
    public void OnClickPresentCharacter()
    {
        if (characterSelectPopup == null) return;
        characterSelectPopup.SetActive(true);
    }

    private void PlayButtonSound()
    {
        if (audioSource != null && audioSource.clip != null)
            audioSource.PlayOneShot(audioSource.clip);
    }

    private void OnCharacterSelected(int index)
    {
        if (characterSelectCamera != null)
            characterSelectCamera.transform.localPosition = new Vector3(-2f * index, 0, 0);

        clientCharIndex = index;
        characterSelectPopup?.SetActive(false);
    }

    public void OnClickOuterPanel()
    {
        characterSelectPopup?.SetActive(false);
    }

    // ========================== Start 버튼 ==========================
    public void OnClickStart()
    {
        if (isConnecting)
        {
            Debug.Log("이미 연결 중입니다...");
            return;
        }

        // 1. 로딩 UI 활성화 (모달 창 띄우기)
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            // (선택 사항) 로딩 애니메이션 시작
            // loadingAnimation?.StartAnimation(); 
        }

        clientName = (nameInput?.text ?? "").Trim();
        if (string.IsNullOrEmpty(clientName))
            clientName = "Player_" + Random.Range(1000, 9999);

#if UNITY_WEBGL && !UNITY_EDITOR
        Screen.fullScreen = true;
        Debug.Log("Entering fullscreen (WebGL)");
#endif

#if UNITY_EDITOR
        ConnectToServer(serverAddress, serverPort, null);
#else
        StartCoroutine(FindGameAndConnect());
#endif
    }

// 연결 실패 시 호출될 함수
public void OnConnectionFailed(string reason)
{
    Debug.LogError($"연결 실패: {reason}");

    // 1. 로딩 UI 비활성화
    if (loadingPanel != null)
    {
        loadingPanel.SetActive(false);
    }
    isConnecting = false;

    // 2. 사용자에게 오류 메시지 표시 (UI)
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
                OnConnectionFailed(req.error);
                isConnecting = false;
                yield break;
            }

            TicketResponse ticket = null;
            try { ticket = JsonUtility.FromJson<TicketResponse>(req.downloadHandler.text); }
            catch { Debug.LogError("Invalid JSON response from FastAPI"); }

            if (ticket == null || !ticket.success)
            {
                Debug.LogError($"find-game returned invalid: {req.downloadHandler.text}");
                OnConnectionFailed(req.error);
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
                try { 
                    status = JsonUtility.FromJson<TicketStatusResponse>(req.downloadHandler.text); 
                }
                catch {
                    Debug.LogError("Invalid ticket status JSON"); 
                }

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
                    OnConnectionFailed($"Matchmaking failed: {status.status}");
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
            OnConnectionFailed($"Matchmaking timeout");
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
            OnConnectionFailed($"NetworkManager not found!");
            isConnecting = false;
            return;
        }

        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("❌ UnityTransport missing on NetworkManager");
            OnConnectionFailed($"missing on NetworkManager");
            isConnecting = false;
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
                transport.UseWebSockets = true;  // WebGL 강제
#endif
        transport.SetConnectionData(serverAddress, serverPort);
        Debug.Log($"Connecting to {serverAddress}:{serverPort} ...");

        //ConnectionData 구성: [1바이트 캐릭터][이름(UTF8 ≤16B)][0x00][playerSessionId UTF8]
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
            OnConnectionFailed($"StartClient failed");
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
            Debug.Log("⏰ Connection timeout!");
            if (NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();
            isConnecting = false;

            OnConnectionFailed("Connection attempt timed out (10s).");
        }
    }
}

// ========================== JSON 구조체 ==========================
[System.Serializable]
public class TicketResponse
{
    public bool success;
    public string ticket_id;
    public string player_id;
    public int poll_interval_sec;
}

[System.Serializable]
public class TicketStatusResponse
{
    public string status;
    public bool success;
    public int retry_after_sec;
    public string server_ip;
    public int server_port;
    public string player_session_id;
    public string game_session_id;
    public string reason;
}
