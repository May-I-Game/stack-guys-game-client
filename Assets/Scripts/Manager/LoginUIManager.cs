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
///  - 게임 서버에 직접 연결
///  - WebGL에서는 WebSocket 모드로 전환
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    // 서버 접속 주소 (에디터: 로컬, 빌드: EC2)
    [SerializeField] public string serverAddress = "127.0.0.1";
    [SerializeField] ushort serverPort = 7779;

    [Header("Production Server")]
    [SerializeField] private string productionServerAddress = "3.37.88.2";
    [SerializeField] private ushort productionServerPort = 7779;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Camera characterSelectCamera;
    [SerializeField] private GameObject characterSelectPopup;

    [Header("Loading UI")]
    [Tooltip("로딩 중 표시할 UI 패널 (캔버스에 미리 배치되어 있어야 함)")]
    public GameObject loadingPanel;

    [Header("Options Button")]
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button closeOptionsButton;

    [Header("Options UI")]
    [SerializeField] private GameObject optionsPanel;   // 옵션창 (Panel)

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
        optionsPanel?.SetActive(false);

        if (closeOptionsButton != null)
            closeOptionsButton.onClick.AddListener(OnClickCloseOptions);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnClickOptionsButton);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

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
                OnConnectionFailed("Client connection attempt failed.");
            }
        }
    }

    // ========================== 옵션 버튼 ==========================
    public void OnClickOptionsButton()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(true);
    }

    public void OnClickCloseOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
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

        // 에디터에서는 로컬 서버, 빌드에서는 프로덕션 서버 연결
#if UNITY_EDITOR
        ConnectToServer(serverAddress, serverPort);
#else
        ConnectToServer(productionServerAddress, productionServerPort);
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

    // ========================== 서버 연결 로직 ==========================
    private void ConnectToServer(string serverAddress, ushort serverPort)
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

        // ✅ Transport 상태 확인
        Debug.Log($"[Transport] Protocol: {transport.Protocol}");
        Debug.Log($"[Transport] UseWebSockets: {transport.UseWebSockets}");

        // ConnectionData 구성: [1바이트 캐릭터][이름(UTF8)]
        byte[] nameBytes = TruncateUtf8(clientName, MAX_NAME_BYTES);
        byte[] payload = new byte[1 + nameBytes.Length];
        payload[0] = (byte)clientCharIndex;
        System.Array.Copy(nameBytes, 0, payload, 1, nameBytes.Length);

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
