using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState
{
    Lobby,
    Playing,
    Ended
}

public class GameManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text gameStartcountdown; // 로비에서 게임 시작 전 카운트
    [SerializeField] private TMP_Text NowPlayerCount; // 현재 접속자 수
    [SerializeField] private TMP_Text gameEndcountdown; // 1등이 결정난 이후 10초 카운트
    [SerializeField] private GameObject resultPanel; // 하얀 결과 화면
    [SerializeField] private TMP_Text firstPlaceText;
    [SerializeField] private TMP_Text secondPlaceText;
    [SerializeField] private TMP_Text thirdPlaceText;
    [SerializeField] private Button mainButton;

    [Header("Settings")]
    [SerializeField] private int minPlayersToStart = 5;
    [SerializeField] private float startCountdownTime = 5f;
    [SerializeField] private float endCountdownTime = 10f;
    [SerializeField] private string mainSceneName = "Login";

    [Header("Spawn Points")]
    [SerializeField] private Transform[] lobbySpawnPoints;
    [SerializeField] private Transform[] gameSpawnPoints;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector timeline;

    [Header("Audio")]
    [SerializeField] private AudioSource lobbyBGM;
    [SerializeField] private AudioSource trackBGM;

    public bool IsLobby => currentGameState.Value == GameState.Lobby;
    public bool IsGame => currentGameState.Value == GameState.Playing;

    private bool isCountingDown = false;
    private Coroutine countdownCoroutine;

    private NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>(GameState.Lobby);
    private NetworkVariable<float> remainingTime = new NetworkVariable<float>(0f);
    private NetworkList<FixedString32Bytes> rankings;

    //시네마틱 실행용 동기화 시간 변수
    private NetworkVariable<double> timelineStartTime = new NetworkVariable<double>(0);
    private NetworkVariable<bool> shouldPlayTimeline = new NetworkVariable<bool>(false);
    private const float SYNC_BUFFER = 0.3f;

    //로비 및 게임 종료 시 사용
    private NetworkVariable<int> currentPlayerCount = new NetworkVariable<int>(0);
    private NetworkVariable<bool> isStartCountdownActive = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isEndCountdownActive = new NetworkVariable<bool>(false);

    public static GameManager instance;

    private void Awake()
    {
        // 싱글톤 패턴
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        rankings = new NetworkList<FixedString32Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        // UI 초기 숨기기
        if (resultPanel != null)
            resultPanel.SetActive(false);
        // 버튼 이벤트 연결
        if (mainButton != null)
            mainButton.onClick.AddListener(GoToMain);

        // 커서 관리
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        if (IsServer)
        {
            currentGameState.Value = GameState.Lobby;

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        if (IsClient)
        {
            remainingTime.OnValueChanged += UpdateCountDownUI;
            shouldPlayTimeline.OnValueChanged += OnTimelineTriggered; // 시네마틱 동기화 변수

            // 플레이어 수 변경 감지
            currentPlayerCount.OnValueChanged += UpdatePlayerCountUI;
            isStartCountdownActive.OnValueChanged += UpdateStartCountdownVisibility;
            isEndCountdownActive.OnValueChanged += UpdateEndCountdownVisibility;

            // 초기 상태 동기화 (새로 접속한 클라이언트를 위해)
            UpdatePlayerCountUI(0, currentPlayerCount.Value);
            UpdateStartCountdownVisibility(false, isStartCountdownActive.Value);
            UpdateEndCountdownVisibility(false, isEndCountdownActive.Value);

            // 카운트다운이 진행 중이면 현재 시간도 업데이트
            if (isStartCountdownActive.Value || isEndCountdownActive.Value)
            {
                UpdateCountDownUI(0, remainingTime.Value);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        if (IsClient)
        {
            remainingTime.OnValueChanged -= UpdateCountDownUI;
            shouldPlayTimeline.OnValueChanged -= OnTimelineTriggered; // 시네마틱 동기화 변수

            // 플레이어 수 변경 감지
            currentPlayerCount.OnValueChanged -= UpdatePlayerCountUI;
            isStartCountdownActive.OnValueChanged -= UpdateStartCountdownVisibility;
            isEndCountdownActive.OnValueChanged -= UpdateEndCountdownVisibility;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        // 새로 접속한 플레이어를 로비 스폰 지점으로 이동
        MovePlayerToLobby(clientId);
        CheckPlayerCount();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        CheckPlayerCount();
    }

    private void MovePlayerToLobby(ulong clientId)
    {
        NetworkObject playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerObject == null) return;

        NetworkTransform nt = playerObject.GetComponent<NetworkTransform>();
        if (nt != null)
        {
            Transform randomSpawnPoint = lobbySpawnPoints[Random.Range(0, lobbySpawnPoints.Length)];
            nt.Teleport(randomSpawnPoint.position, Quaternion.identity, playerObject.transform.localScale);
        }
    }

    private void CheckPlayerCount()
    {
        // 로비에서만 실행
        if (currentGameState.Value != GameState.Lobby) return;

        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        currentPlayerCount.Value = playerCount;
        if (playerCount >= minPlayersToStart && !isCountingDown)
        {
            // 카운트다운 시작
            isCountingDown = true;
            isStartCountdownActive.Value = true;
            countdownCoroutine = StartCoroutine(StartGameCountdown());

        }
        else if (playerCount < minPlayersToStart && isCountingDown)
        {
            isCountingDown = false;
            isStartCountdownActive.Value = false;
            // 카운트다운 중지
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
            }
        }
    }
    private void UpdatePlayerCountUI(int priviousValue, int newValue)
    {
        if (NowPlayerCount != null)
        {
            NowPlayerCount.text = $"현재 참가자: {newValue}명";
        }
    }
    private void UpdateStartCountdownVisibility(bool previousValue, bool newValue)
    {
        if (gameStartcountdown != null)
        {
            gameStartcountdown.gameObject.SetActive(newValue);
        }
    }
    private void UpdateEndCountdownVisibility(bool previousValue, bool newValue)
    {
        if (gameEndcountdown != null)
        {
            gameEndcountdown.gameObject.SetActive(newValue);
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void PlayerReachedGoalServerRpc(string playerName, ulong clientId)
    {
        if (currentGameState.Value != GameState.Playing) return;

        NetworkObject playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerObject == null) return;

        PlayerController player = playerObject.GetComponent<PlayerController>();
        if (player != null)
        {
            player.enabled = false;
        }

        // 순위에 추가
        rankings.Add(playerName);

        // 첫 번째 플레이어가 골인하면 카운트다운 시작
        if (rankings.Count == 1 && !isCountingDown)
        {
            isEndCountdownActive.Value = true;
            StartCoroutine(EndGameCountdown());
        }
    }

    private IEnumerator StartGameCountdown()
    {
        isCountingDown = true;

        // 게임 시작 카운트다운
        remainingTime.Value = startCountdownTime;
        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;
            yield return null;
        }

        isCountingDown = false;

        // 게임 시작 처리
        StartGame();
    }

    private IEnumerator EndGameCountdown()
    {
        isCountingDown = true;

        // 게임 종료 카운트다운
        remainingTime.Value = endCountdownTime;
        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;
            yield return null;
        }

        isCountingDown = false;

        // 게임 종료 처리
        EndGame();
    }

    private void StartGame()
    {
        if (!IsServer) return;

        currentGameState.Value = GameState.Playing;

        HideLobbyUIClientRpc();

        int i = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            NetworkTransform nt = playerObject.GetComponent<NetworkTransform>();
            if (nt == null) continue;

            // 순환하면서 스폰 위치 지정
            Vector3 spawnPos = gameSpawnPoints[i % gameSpawnPoints.Length].position;

            // 해당 플레이어에게 텔레포트 명령
            nt.Teleport(spawnPos, Quaternion.identity, playerObject.transform.localScale);

            i++;
        }
        timelineStartTime.Value = NetworkManager.Singleton.ServerTime.Time + SYNC_BUFFER;
        shouldPlayTimeline.Value = true;
    }

    private void EndGame()
    {
        if (!IsServer) return;

        currentGameState.Value = GameState.Ended;

        // 클라에 결과 화면 표시
        ShowResultsClientRpc();
    }

    [ClientRpc]
    private void ShowResultsClientRpc()
    {
        //카운트 다운 내리기
        gameEndcountdown.gameObject.SetActive(false);
        // 결과 화면 표시
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        // 커서 보이기
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 순위 표시
        UpdateRankingUI();
    }
    [ClientRpc]
    private void HideLobbyUIClientRpc()
    {
        if (gameStartcountdown != null)
        {
            gameStartcountdown.gameObject.SetActive(false);
        }
        if (NowPlayerCount != null)
        {
            NowPlayerCount.gameObject.SetActive(false);
        }
    }
    private void UpdateCountDownUI(float prviousValue, float newValue)
    {
        // 카운트 다운
        if (rankings.Count > 0 && gameEndcountdown != null)
        {
            gameEndcountdown.text = Mathf.Ceil(newValue).ToString();
        }
        else if (gameStartcountdown != null)
        {
            gameStartcountdown.text = $"{Mathf.Ceil(newValue)}초 후 게임이 시작됩니다!!";
        }
    }

    private void UpdateRankingUI()
    {
        // 1등
        if (rankings.Count > 0 && firstPlaceText != null)
        {
            firstPlaceText.text = $"1.: {rankings[0]}";
        }

        // 2등
        if (rankings.Count > 1 && secondPlaceText != null)
        {
            secondPlaceText.text = $"2.: {rankings[1]}";
        }
        else if (secondPlaceText != null)
        {
            secondPlaceText.text = "2.: -";
        }

        // 3등
        if (rankings.Count > 2 && thirdPlaceText != null)
        {
            thirdPlaceText.text = $"3.: {rankings[2]}";
        }
        else if (thirdPlaceText != null)
        {
            thirdPlaceText.text = "3.: -";
        }
    }

    private void GoToMain()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(mainSceneName);
    }
    private void OnTimelineTriggered(bool previous, bool current)
    {
        if (current && !previous)
        {
            StartCoroutine(PlayTimelineAtSyncTime());
        }
    }
    private IEnumerator PlayTimelineAtSyncTime()
    {
        //입력 차단
        DisablePlayerInput();

        // 유저의 입력 벡터 모두 초기화 (ClientRpc 호출)
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            PlayerController playerController = playerObject.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.ResetInputClientRpc();
            }
        }

        //로비 BGM 아웃
        lobbyBGM.Stop();

        // 네트워크 시간과 동기화
        while (NetworkManager.Singleton.ServerTime.Time < timelineStartTime.Value)
        {
            yield return null;
        }


        //timeline재생
        timeline.Play();
        //트랙 bgm on
        trackBGM.Play();

        //Timeline종료 대기
        yield return new WaitForSeconds((float)timeline.duration);

        //입력 활성화
        EnablePlayerInput();
    }
    private void OnTimelineFinished(PlayableDirector director)
    {
        director.stopped -= OnTimelineFinished;

        EnablePlayerInput();

        Debug.Log("Timeline 종료, 게임 플레이 시작");
    }
    private void DisablePlayerInput()
    {
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.SetInputEnabled(false);
        }
    }
    private void EnablePlayerInput()
    {
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            localPlayer.SetInputEnabled(true);
        }
    }
    private PlayerController GetLocalPlayer()
    {
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                return player;
            }

        }
        return null;
    }
}