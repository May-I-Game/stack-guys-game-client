using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// 네트워크 통계 표시 (FPS, Ping)
/// 50ms 주기로 UI 업데이트
/// </summary>
public class NetworkStatsDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text fpsText;
    [SerializeField] private TMP_Text pingText;

    [Header("Settings")]
    [Tooltip("UI 업데이트 주기 (초). 권장: 0.5 (500ms)")]
    [SerializeField] private float updateInterval = 0.5f;  // 500ms = 2Hz

    [Tooltip("FPS 평균 계산 샘플 수")]
    [SerializeField] private int fpsSamples = 30;

    [Tooltip("Ping 평균 계산 샘플 수")]
    [SerializeField] private int pingSamples = 30;

    // 내부 변수
    private float nextUpdateTime = 0f;
    private NetworkManager networkManager;
    private UnityTransport unityTransport;

    // FPS 계산
    private float[] fpsHistory;
    private int fpsHistoryIndex = 0;
    private float currentFPS = 0f;

    // Ping 계산
    private float[] pingHistory;
    private int pingHistoryIndex = 0;
    private float currentPing = 0f;

    void Start()
    {
        // 히스토리 배열 초기화
        fpsHistory = new float[fpsSamples];
        pingHistory = new float[pingSamples];

        // NetworkManager 찾기
        networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            unityTransport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        }

        // 초기 UI 설정
        if (fpsText != null)
            fpsText.text = "FPS: --";

        if (pingText != null)
            pingText.text = "Ping: --";
    }

    void Update()
    {
        // 매 프레임 통계 수집 (정확한 측정)
        CollectStats();

        // 50ms마다 UI 업데이트
        if (Time.time >= nextUpdateTime)
        {
            UpdateUI();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    /// <summary>
    /// 통계 수집 (매 프레임)
    /// </summary>
    private void CollectStats()
    {
        // FPS 계산
        float fps = 1f / Time.unscaledDeltaTime;
        fpsHistory[fpsHistoryIndex] = fps;
        fpsHistoryIndex = (fpsHistoryIndex + 1) % fpsSamples;

        // FPS 평균 계산
        float totalFPS = 0f;
        for (int i = 0; i < fpsSamples; i++)
        {
            totalFPS += fpsHistory[i];
        }
        currentFPS = totalFPS / fpsSamples;

        // Ping 수집 (클라이언트만)
        if (networkManager != null && networkManager.IsClient && !networkManager.IsServer)
        {
            if (unityTransport != null)
            {
                try
                {
                    float ping = unityTransport.GetCurrentRtt(0);
                    pingHistory[pingHistoryIndex] = ping;
                    pingHistoryIndex = (pingHistoryIndex + 1) % pingSamples;

                    // Ping 평균 계산
                    float totalPing = 0f;
                    for (int i = 0; i < pingSamples; i++)
                    {
                        totalPing += pingHistory[i];
                    }
                    currentPing = totalPing / pingSamples;
                }
                catch
                {
                    // Ping 측정 실패 시 무시
                }
            }
        }
    }

    /// <summary>
    /// UI 업데이트 (50ms 주기)
    /// </summary>
    private void UpdateUI()
    {
        // FPS 업데이트
        if (fpsText != null)
        {
            fpsText.text = $"FPS: {currentFPS:F1}";

            // FPS에 따라 색상 변경 (선택사항)
            if (currentFPS >= 55f)
                fpsText.color = Color.green;
            else if (currentFPS >= 30f)
                fpsText.color = Color.yellow;
            else
                fpsText.color = Color.red;
        }

        // Ping 업데이트 (클라이언트만)
        if (pingText != null)
        {
            if (networkManager != null && networkManager.IsClient && !networkManager.IsServer)
            {
                pingText.text = $"Ping: {currentPing:F0}ms";

                // Ping에 따라 색상 변경 (선택사항)
                if (currentPing <= 100f)
                    pingText.color = Color.green;
                else if (currentPing <= 200f)
                    pingText.color = Color.yellow;
                else
                    pingText.color = Color.red;
            }
            else if (networkManager != null && networkManager.IsServer)
            {
                pingText.text = "Ping: Server";
                pingText.color = Color.white;
            }
            else
            {
                pingText.text = "Ping: --";
                pingText.color = Color.gray;
            }
        }
    }

    /// <summary>
    /// 공개 API: 현재 FPS 가져오기
    /// </summary>
    public float GetCurrentFPS()
    {
        return currentFPS;
    }

    /// <summary>
    /// 공개 API: 현재 Ping 가져오기
    /// </summary>
    public float GetCurrentPing()
    {
        return currentPing;
    }

    /// <summary>
    /// 공개 API: UI 업데이트 주기 변경
    /// </summary>
    public void SetUpdateInterval(float interval)
    {
        updateInterval = Mathf.Max(0.016f, interval);  // 최소 16ms (60Hz)
    }
}
