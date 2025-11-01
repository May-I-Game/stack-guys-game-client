using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] TMP_Text countdownText; // 화면 중앙의 카운트다운
    [SerializeField] GameObject resultPanel; // 하얀 결과 화면
    [SerializeField] TMP_Text firstPlaceText;
    [SerializeField] TMP_Text secondPlaceText;
    [SerializeField] TMP_Text thirdPlaceText;
    [SerializeField] Button lobbyButton;

    [Header("Settings")]
    [SerializeField] float countdownTime = 10f;
    [SerializeField] string lobbySceneName = "LobbyScene";

    private bool isCountingDown = false;

    public NetworkVariable<bool> gameEnded = new NetworkVariable<bool>(false);
    public NetworkVariable<float> remainingTime = new NetworkVariable<float>(0f);
    public NetworkList<FixedString32Bytes> rankings;

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
        if (lobbyButton != null)
            lobbyButton.onClick.AddListener(GoToLobby);

        // 커서 관리
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        if (!IsServer)
        {
            remainingTime.OnValueChanged += UpdateCountDownUI;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            remainingTime.OnValueChanged -= UpdateCountDownUI;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayerReachedGoalServerRpc(string playerName, ulong clientId)
    {
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.OwnerClientId == clientId)
            {
                player.enabled = false;
            }
        }

        // 순위에 추가
        rankings.Add(playerName);

        // 첫 번째 플레이어가 골인하면 카운트다운 시작
        if (rankings.Count == 1 && !isCountingDown)
        {
            StartCoroutine(CountdownRoutine());
        }
    }

    private IEnumerator CountdownRoutine()
    {
        isCountingDown = true;

        ShowCountDownClientRpc();

        // 10초 카운트다운
        remainingTime.Value = countdownTime;
        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;
            yield return null;
        }

        HideCountDownClientRpc();

        // 게임 종료 처리
        EndGame();
    }

    private void EndGame()
    {
        if (!IsServer) return;

        gameEnded.Value = true;

        // 모든 플레이어 컨트롤 비활성화
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            player.enabled = false;
        }

        // 클라에 결과 화면 표시
        ShowResultsClientRpc();
    }

    [ClientRpc]
    private void ShowCountDownClientRpc()
    {
        // 카운트다운 텍스트 표시
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }
    }

    [ClientRpc]
    private void HideCountDownClientRpc()
    {
        // 카운트다운 종료
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
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

    private void GoToLobby()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(lobbySceneName);
    }

}