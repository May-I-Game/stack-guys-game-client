using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
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
    [SerializeField] private TMP_Text countdownText; // 화면 중앙의 카운트다운
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
    [SerializeField] private Transform lobbySpawnPoint;
    [SerializeField] private Transform[] gameSpawnPoints;

    public bool IsLobby => currentGameState.Value == GameState.Lobby;
    public bool IsGame => currentGameState.Value == GameState.Playing;

    private bool isCountingDown = false;
    private Coroutine countdownCoroutine;

    private NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>(GameState.Lobby);
    private NetworkVariable<float> remainingTime = new NetworkVariable<float>(0f);
    private NetworkList<FixedString32Bytes> rankings;

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
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
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
            nt.Teleport(lobbySpawnPoint.transform.position, Quaternion.identity, playerObject.transform.localScale);
        }
    }

    private void CheckPlayerCount()
    {
        // 로비에서만 실행
        if (currentGameState.Value != GameState.Lobby) return;

        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (playerCount >= minPlayersToStart && !isCountingDown)
        {
            // 카운트다운 시작
            isCountingDown = true;
            ShowCountDownClientRpc(true);
            countdownCoroutine = StartCoroutine(StartGameCountdown());
        }
        else if (playerCount < minPlayersToStart && isCountingDown)
        {
            isCountingDown = false;
            ShowCountDownClientRpc(false);
            // 카운트다운 중지
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
            }
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
            StartCoroutine(EndGameCountdown());
        }
    }

    private IEnumerator StartGameCountdown()
    {
        isCountingDown = true;
        ShowCountDownClientRpc(true);

        // 게임 시작 카운트다운
        remainingTime.Value = startCountdownTime;
        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;
            yield return null;
        }

        isCountingDown = false;
        ShowCountDownClientRpc(false);

        // 게임 시작 처리
        StartGame();
    }

    private IEnumerator EndGameCountdown()
    {
        isCountingDown = true;
        ShowCountDownClientRpc(true);

        // 게임 종료 카운트다운
        remainingTime.Value = endCountdownTime;
        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;
            yield return null;
        }

        isCountingDown = false;
        ShowCountDownClientRpc(false);

        // 게임 종료 처리
        EndGame();
    }

    private void StartGame()
    {
        if (!IsServer) return;

        currentGameState.Value = GameState.Playing;

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
    }

    private void EndGame()
    {
        if (!IsServer) return;

        currentGameState.Value = GameState.Ended;

        // 클라에 결과 화면 표시
        ShowResultsClientRpc();
    }

    [ClientRpc]
    private void ShowCountDownClientRpc(bool active)
    {
        // 카운트다운 텍스트 표시
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(active);
        }
    }

    [ClientRpc]
    private void ShowResultsClientRpc()
    {
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

    private void UpdateCountDownUI(float prviousValue, float newValue)
    {
        // 카운트 다운
        if (countdownText != null)
        {
            countdownText.text = Mathf.Ceil(newValue).ToString();
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

}