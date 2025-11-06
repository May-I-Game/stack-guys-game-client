using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// LoginUIManager
///  - FastAPI를 통해 GameLift 매치메이킹 요청
///  - 서버 IP/Port/PlayerSessionId 수신 후 UnityTransport로 연결
///  - WebGL에서는 WebSocket 모드로 전환
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    [SerializeField] private string matchApiUrl = "http://54.180.24.20/api/find-game"; // FastAPI 주소
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Camera characterSelectCamera;
    [SerializeField] private GameObject characterSelectPopup;

    private int clientCharIndex;
    private string clientName;
    private bool isConnecting = false;
    private AudioSource audioSource;

    private const int MAX_NAME_BYTES = 16;

    void Start()
    {
        characterSelectPopup?.SetActive(false);
        audioSource = GetComponent<AudioSource>();

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
            }
        }
    }

    // ========================== 캐릭터 선택 ==========================
    public void OnClickPresentCharacter()
    {
        if (characterSelectPopup == null) return;
        characterSelectPopup.SetActive(true);

        Button[] buttons = characterSelectPopup.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => OnCharacterSelected(index));

            var trigger = buttons[i].GetComponent<EventTrigger>() ?? buttons[i].gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => PlayButtonSound());
            trigger.triggers.Add(entry);
        }
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

        clientName = (nameInput?.text ?? "").Trim();
        if (string.IsNullOrEmpty(clientName))
            clientName = "Player_" + Random.Range(1000, 9999);

#if UNITY_WEBGL && !UNITY_EDITOR
        Screen.fullScreen = true;
        Debug.Log("Entering fullscreen (WebGL)");
#endif

        StartCoroutine(FindGameAndConnect());
    }

    // ========================== FastAPI 매치 요청 ==========================
    private IEnumerator FindGameAndConnect()
    {
        isConnecting = true;
        Debug.Log("🎮 Finding game server via FastAPI…");

        var req = new UnityWebRequest(matchApiUrl, "POST");
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

        GameServerInfo info = null;
        try { info = JsonUtility.FromJson<GameServerInfo>(req.downloadHandler.text); }
        catch { Debug.LogError("Invalid JSON response from FastAPI"); }

        if (info == null || !info.success)
        {
            Debug.LogError($"find-game returned invalid: {req.downloadHandler.text}");
            isConnecting = false;
            yield break;
        }

        Debug.Log($"Got server info → {info.server_ip}:{info.server_port}");
        ConnectToServer(info.server_ip, (ushort)info.server_port, info.player_session_id);
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
            Debug.Log("⏰ Connection timeout!");
            if (NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();
            isConnecting = false;
        }
    }
}

// ========================== JSON 구조체 ==========================
[System.Serializable]
public class GameServerInfo
{
    public bool success;
    public string server_ip;
    public int server_port;
    public string player_session_id;
    public string game_session_id;
}
