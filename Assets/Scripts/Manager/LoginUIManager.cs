using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class LoginUIManager : MonoBehaviour
{
    [SerializeField] public string serverAddress = "127.0.0.1";
    [SerializeField] ushort serverPort = 7779;
    [SerializeField] private TMP_InputField nameInput; // 닉네임 입력하는 inputtext
    [SerializeField] private Camera characterSelectCamera; // 캐릭터 선택 시에 현재 캐릭터를 보여줄 카메라
    [SerializeField] private GameObject characterSelectPopup; //캐릭터 선택 팝업 창

    private int clientCharIndex; // 클라이언트가 선택한 캐릭터 인덱스
    private string clientName; // 클라이언트가 작성한 이름
    private bool isConnecting = false; //중복 클릭 방지

    private AudioSource audioSource;

    private const int MAX_NAME_LENGTH = 16;  //이름 글자수 제한 16byte
    void Start()
    {
        characterSelectPopup.SetActive(false); // 캐릭터 팝업창 닫아두기

        audioSource = GetComponent<AudioSource>();

        if (NetworkManager.Singleton != null)
        {
            //networkManager 콜백 구독
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
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
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        CancelInvoke(nameof(CheckConnectionTimeout));
    }
    // 현재 화면에 나와있는 캐릭터를 클릭했을 경우
    public void OnClickPresentCharacter()
    {
        characterSelectPopup.SetActive(true);
        // Grid의 모든 자식에서 Button 컴포넌트 찾기
        Button[] buttons = characterSelectPopup.GetComponentsInChildren<Button>();

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i; // 클로저 문제 방지 (중요!)

            // 기존 이벤트 제거 후 새로 추가
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() => OnCharacterSelected(index));

            // PointerDown 이벤트 추가 (소리용)
            EventTrigger trigger = buttons[i].GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = buttons[i].gameObject.AddComponent<EventTrigger>();
            }

            // 기존 이벤트 제거
            trigger.triggers.Clear();

            // PointerDown 이벤트 추가
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerDown;
            entry.callback.AddListener((data) => { PlayButtonSound(); });
            trigger.triggers.Add(entry);
        }
    }
    private void PlayButtonSound()
    {
        if (audioSource != null)
        {
            audioSource.PlayOneShot(audioSource.clip);
        }
    }
    //팝업의 캐릭터를 클릭했을 경우
    private void OnCharacterSelected(int index)
    {
        //카메라 x위치 변경 : -2 * index
        characterSelectCamera.transform.localPosition = new Vector3(-2f * index, 0, 0);

        // 인덱스 받아오기
        clientCharIndex = index;

        // 팝업 닫기
        characterSelectPopup.SetActive(false);
    }
    // 팝업밖의 패널을 클릭했을 경우
    public void OnClickOuterPanel()
    {
        characterSelectPopup.SetActive(false);
    }
    // Start 버튼 눌렀을 경우, 캐릭터 index와 name을 서버로 전송 및 websocket연결
    public void OnClickStart()
    {
        //중복 클릭 방지
        if (isConnecting)
        {
            Debug.Log("이미 연결 중입니다...");
        }

        //inputtext 기반 이름 설정
        clientName = (nameInput?.text ?? "").Trim();

        //이름 글자 수 제한을 넘겼을 경우 16byte로 truncate
        if (clientName.Length > MAX_NAME_LENGTH)
        {
            clientName = clientName.Substring(0, MAX_NAME_LENGTH);
        }

        if (string.IsNullOrEmpty(clientName))
        {
            //입력값이 없을 경우
            clientName = "Player_" + Random.Range(1000, 9999);
        }
        ConnectToServer();
    }
    private void ConnectToServer()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.Log("NetworkManager not found!");
            isConnecting = false;
            return;
        }
        // 서버 주소 설정
        // #if UNITY_EDITOR
        // #else
        //         const string serverAddress = "54.180.159.66";
        //         const ushort serverPort = 7779;
        // #endif

        Debug.Log($"Connecting to {serverAddress}:{serverPort}...");

        //Unitytransport 설정
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(serverAddress, serverPort);
        }
        else
        {
            Debug.Log("unityTransport not found");
            return;
        }

        isConnecting = true;

        //서버로 캐릭터 인덱스를 보내기
        byte[] payload = new byte[17];

        payload[0] = (byte)clientCharIndex;
        // 이름을 ASCII 바이트로 변환
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(clientName);

        // 이름 복사 (최대 16바이트)
        int bytesToCopy = Mathf.Min(nameBytes.Length, MAX_NAME_LENGTH);
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
