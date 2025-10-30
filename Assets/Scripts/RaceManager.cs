using Unity.Netcode;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

public class RaceManager : NetworkBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector introTimeline;

    [Header("Cameras")]
    [SerializeField] private CinemachineVirtualCamera vcamMapOverview;
    [SerializeField] private CinemachineVirtualCamera vcamStartLine;
    [SerializeField] private CinemachineVirtualCamera vcamPlayer;

    [Header("UI")]
    [SerializeField] private GameObject countdownUI;
    [SerializeField] private TMPro.TextMeshProUGUI countdownText;

    [Header("Player")]
    [SerializeField] private Transform localPlayerTransform;

    // 네트워크 동기화된 카운트다운
    private NetworkVariable<float> raceCountdown = new NetworkVariable<float>(
        3f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> raceStarted = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 서버: 모든 플레이어 준비되면 시작
            StartRaceIntro();
        }

        // 클라이언트: Timeline 재생
        if (introTimeline != null)
        {
            introTimeline.Play();
        }

        // 카운트다운 변경 감지
        raceCountdown.OnValueChanged += OnCountdownChanged;
        raceStarted.OnValueChanged += OnRaceStartedChanged;
    }

    private void StartRaceIntro()
    {
        // 5초 후 카운트다운 시작 (인트로 타임라인 길이)
        Invoke(nameof(StartCountdown), 5f);
    }

    private void StartCountdown()
    {
        if (!IsServer) return;

        // 서버에서 카운트다운 시작
        InvokeRepeating(nameof(UpdateCountdown), 0f, 1f);
    }

    private void UpdateCountdown()
    {
        if (!IsServer) return;

        raceCountdown.Value -= 1f;

        if (raceCountdown.Value <= 0)
        {
            CancelInvoke(nameof(UpdateCountdown));
            raceStarted.Value = true;
        }
    }

    private void OnCountdownChanged(float oldValue, float newValue)
    {
        // 모든 클라이언트에서 UI 업데이트
        if (newValue > 0)
        {
            countdownUI?.SetActive(true);
            countdownText.text = Mathf.CeilToInt(newValue).ToString();
        }
        else
        {
            countdownText.text = "GO!";
            Invoke(nameof(HideCountdownUI), 0.5f);
        }
    }

    private void OnRaceStartedChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            OnRaceStart();
        }
    }

    private void OnRaceStart()
    {
        Debug.Log("Race Started!");

        // 플레이어 카메라로 전환
        SwitchToPlayerCamera();

        // 플레이어 컨트롤 활성화
        EnablePlayerControls();
    }

    private void SwitchToPlayerCamera()
    {
        // 로컬 플레이어 찾기
        if (localPlayerTransform == null)
        {
            localPlayerTransform = FindLocalPlayer();
        }

        if (localPlayerTransform != null && vcamPlayer != null)
        {
            // 플레이어 추적 설정
            vcamPlayer.Follow = localPlayerTransform;
            vcamPlayer.LookAt = localPlayerTransform;

            // 우선순위 변경으로 카메라 전환
            vcamMapOverview.Priority = 0;
            vcamStartLine.Priority = 0;
            vcamPlayer.Priority = 20;
        }
    }

    private Transform FindLocalPlayer()
    {
        // 로컬 플레이어 NetworkObject 찾기
        foreach (var player in FindObjectsOfType<NetworkObject>())
        {
            if (player.IsOwner && player.CompareTag("Player"))
            {
                return player.transform;
            }
        }
        return null;
    }

    private void EnablePlayerControls()
    {
        // // 플레이어 입력 활성화
        // var playerController = localPlayerTransform?.GetComponent<PlayerController>();
        // if (playerController != null)
        // {
        //     playerController.EnableControls();
        // }
    }

    private void HideCountdownUI()
    {
        countdownUI?.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        raceCountdown.OnValueChanged -= OnCountdownChanged;
        raceStarted.OnValueChanged -= OnRaceStartedChanged;
    }
}