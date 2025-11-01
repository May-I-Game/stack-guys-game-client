using Amazon.GameLift.Model;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
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

    private List<string> rankings = new List<string>();
    private bool isCountingDown = false;
    public bool gameEnded { get; private set; } = false;

    public static GameManager instance;

    private void Awake()
    {
        // 싱글톤 패턴
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 초기 UI 숨기기
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 버튼 이벤트 연결
        if (lobbyButton != null)
            lobbyButton.onClick.AddListener(GoToLobby);
    }

    // 커서 관리
    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void PlayerReachedGoal(string playerName)
    {
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

        // 카운트다운 텍스트 표시
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }

        // 10초 카운트다운
        float remainingTime = countdownTime;
        while (remainingTime > 0)
        {
            if (countdownText != null)
            {
                countdownText.text = Mathf.Ceil(remainingTime).ToString();
            }

            remainingTime -= Time.deltaTime;
            yield return null;
        }

        // 카운트다운 종료
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // 게임 종료 처리
        EndGame();
    }

    private void EndGame()
    {
        // 모든 플레이어 컨트롤 비활성화
        DisableAllPlayerControls();

        // 결과 화면 표시
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        // 커서 보이기
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 순위 표시
        DisplayRankings();
    }

    private void DisableAllPlayerControls()
    {
        // PlayerController 비활성화
        PlayerController[] pcPlayers = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in pcPlayers)
        {
            player.enabled = false;
        }
    }

    private void DisplayRankings()
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
        gameEnded = false;
        SceneManager.LoadScene(lobbySceneName);
    }

}