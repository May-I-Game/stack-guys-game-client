using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameEndManager : MonoBehaviour
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
        // ThirdPersonController 비활성화
        StarterAssets.ThirdPersonController[] players = Object.FindObjectsByType<StarterAssets.ThirdPersonController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.enabled = false;
        }

        // StarterAssetsInputs 비활성화 (입력 차단)
        StarterAssets.StarterAssetsInputs[] inputs = Object.FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsSortMode.None);
        foreach (var input in inputs)
        {
            input.enabled = false;
        }

        // CharacterController 비활성화 (움직임 차단)
        CharacterController[] controllers = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            controller.enabled = false;
        }

#if ENABLE_INPUT_SYSTEM
        // PlayerInput 비활성화 (New Input System 차단)
        UnityEngine.InputSystem.PlayerInput[] playerInputs = Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(FindObjectsSortMode.None);
        foreach (var playerInput in playerInputs)
        {
            playerInput.enabled = false;
        }
#endif
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

    public void GoToLobby()
    {
        GoalFlag.ResetGame();
        SceneManager.LoadScene(lobbySceneName);
    }
}