using System;
using System.Reflection;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// ClientOnlyProfiler - 클라이언트 전용 Unity Netcode 프로파일러
/// 
/// [추적 항목]
/// - FPS (초당 프레임)
/// - Ping (RTT, 서버와의 왕복 시간)
/// - 네트워크 패킷 송신량 (KB/s)
/// 
/// [사용 방법]
/// 1. 클라이언트 씬의 NetworkManager GameObject에 이 컴포넌트 추가
/// 2. Inspector에서 Ping Text와 FPS Text에 TextMeshPro 오브젝트 연결
/// 3. 게임 실행 시 자동으로 500ms마다 UI 업데이트
/// 
/// [중요]
/// - 이 스크립트는 클라이언트에서만 작동합니다
/// - 서버(headless 포함)에서는 자동으로 비활성화됩니다
/// </summary>
public class ClientOnlyProfiler : MonoBehaviour
{
    [Header("UI 설정")]
    [Tooltip("핑을 표시할 TextMeshPro 텍스트")]
    [SerializeField] private TMP_Text pingText;

    [Tooltip("FPS를 표시할 TextMeshPro 텍스트")]
    [SerializeField] private TMP_Text fpsText;

    [Tooltip("패킷 송신량을 표시할 TextMeshPro 텍스트 (선택사항)")]
    [SerializeField] private TMP_Text networkText;

    [Header("설정")]
    [Tooltip("UI 업데이트 주기 (초 단위, 기본 0.5초)")]
    [SerializeField] private float updateInterval = 0.5f;

    [Tooltip("핑 측정 주기 (초 단위, 기본 0.3초)")]
    [SerializeField] private float pingInterval = 0.3f;

    [Header("색상 설정")]
    [Tooltip("FPS/핑에 따른 색상 변경 활성화")]
    [SerializeField] private bool enableColorCoding = true;

    // 내부 변수
    private NetworkManager nm;
    private UnityTransport utp;

    // Reflection을 통한 UTP 통계 접근
    private FieldInfo statsField;
    private Type statsType;

    // 통계 변수
    private float currentFPS = 0f;
    private float currentPing = 0f;
    private float currentSendRate = 0f; // KB/s

    // 업데이트 타이머
    private float uiUpdateTimer = 0f;
    private float pingTimer = 0f;

    // 네트워크 통계 추적
    private ulong lastSentBytes = 0;
    private float lastCheckTime = 0f;

    // FPS 계산용
    private float fpsAccumulator = 0f;
    private int fpsFrameCount = 0;

    // 초기화 완료 플래그
    private bool isInitialized = false;

    void Start()
    {
        // NetworkManager 가져오기
        nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[ClientOnlyProfiler] NetworkManager를 찾을 수 없습니다!");
            enabled = false;
            return;
        }

        // 서버에서는 이 스크립트 비활성화 (headless 서버 포함)
        if (nm.IsServer)
        {
            Debug.Log("[ClientOnlyProfiler] 서버에서는 실행되지 않습니다. 스크립트를 비활성화합니다.");
            enabled = false;
            return;
        }

        // 클라이언트 연결 이벤트 구독
        nm.OnClientConnectedCallback += OnClientConnected;

        // 이미 연결되어 있으면 즉시 초기화
        if (nm.IsClient && nm.IsConnectedClient)
        {
            InitializeProfiler();
        }
    }

    void OnDestroy()
    {
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    /// <summary>
    /// 클라이언트가 서버에 연결되면 프로파일러 초기화
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        // 로컬 클라이언트만 초기화
        if (clientId == nm.LocalClientId && !isInitialized)
        {
            InitializeProfiler();
        }
    }

    /// <summary>
    /// 프로파일러 초기화
    /// </summary>
    private void InitializeProfiler()
    {
        // UnityTransport 초기화
        utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp != null)
        {
            try
            {
                // Reflection으로 UTP 내부 통계 필드 접근
                // Unity Netcode 버전에 따라 필드명이 다를 수 있으므로 여러 이름을 시도
                string[] possibleFieldNames = { "m_NetworkMetrics", "m_Statistics", "m_Stats" };

                foreach (string fieldName in possibleFieldNames)
                {
                    statsField = typeof(UnityTransport).GetField(fieldName,
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (statsField != null)
                    {
                        Debug.Log($"[ClientOnlyProfiler] 통계 필드 '{fieldName}' 발견!");
                        break;
                    }
                }

                if (statsField != null)
                {
                    statsType = statsField.FieldType;
                    Debug.Log($"[ClientOnlyProfiler] 프로파일러 초기화 완료! statsType = {statsType.Name}");

                    // NetworkMetrics 내부 필드 출력
                    Debug.Log($"[ClientOnlyProfiler] === {statsType.Name} 내부 필드 목록 ===");
                    foreach (var field in statsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        Debug.Log($"  - {field.Name} ({field.FieldType.Name})");
                    }
                }
                else
                {
                    Debug.LogWarning("[ClientOnlyProfiler] UTP 통계 필드를 찾을 수 없습니다. 네트워크 송신량은 표시되지 않습니다.");
                }

                // 통계 필드 유무와 관계없이 FPS/Ping은 작동하도록 초기화
                isInitialized = true;
                lastCheckTime = Time.time;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClientOnlyProfiler] UTP Reflection 오류: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[ClientOnlyProfiler] UnityTransport를 찾을 수 없습니다.");
        }
    }

    void Update()
    {
        // NetworkManager가 없거나 초기화 안 됐거나 클라이언트가 아니면 리턴
        if (nm == null || !isInitialized || !nm.IsClient) return;

        // FPS 계산 (매 프레임)
        UpdateFPS();

        // 핑 측정 (pingInterval마다)
        pingTimer += Time.deltaTime;
        if (pingTimer >= pingInterval)
        {
            pingTimer = 0f;
            UpdatePing();
        }

        // 네트워크 송신량 계산
        UpdateNetworkStats();

        // UI 업데이트 (updateInterval마다)
        uiUpdateTimer += Time.deltaTime;
        if (uiUpdateTimer >= updateInterval)
        {
            uiUpdateTimer = 0f;
            UpdateUI();
        }
    }

    /// <summary>
    /// FPS 계산 (평균 FPS)
    /// </summary>
    private void UpdateFPS()
    {
        fpsAccumulator += Time.unscaledDeltaTime;
        fpsFrameCount++;

        // updateInterval마다 평균 FPS 계산
        if (fpsAccumulator >= updateInterval)
        {
            currentFPS = fpsFrameCount / fpsAccumulator;
            fpsAccumulator = 0f;
            fpsFrameCount = 0;
        }
    }

    /// <summary>
    /// 핑(RTT) 업데이트 - NetworkManager.NetworkTimeSystem 사용
    /// </summary>
    private void UpdatePing()
    {
        if (utp == null || nm == null) return;

        try
        {
            // Unity Netcode의 NetworkTimeSystem을 사용한 RTT 측정
            // ServerTime과 LocalTime의 차이를 이용
            if (nm.NetworkTimeSystem != null && nm.IsConnectedClient)
            {
                // NetworkTimeSystem의 RTT 값 사용 (밀리초 단위)
                // Netcode는 내부적으로 시간 동기화를 위해 RTT를 추적함
                currentPing = (float)(nm.NetworkTimeSystem.LocalTime - nm.NetworkTimeSystem.ServerTime) * 1000f;

                // 음수이거나 비정상적인 값이면 GetCurrentRtt로 대체
                if (currentPing < 0 || currentPing > 5000)
                {
                    currentPing = utp.GetCurrentRtt(0ul);
                }
            }
            else
            {
                // NetworkTimeSystem을 사용할 수 없으면 GetCurrentRtt 사용
                currentPing = utp.GetCurrentRtt(0ul);
            }

            // 디버그: RTT 값 출력 (5초마다)
            if (Time.frameCount % 300 == 0)
            {
                Debug.Log($"[ClientOnlyProfiler] Ping = {currentPing}ms, LocalClientId = {nm.LocalClientId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ClientOnlyProfiler] RTT 가져오기 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 네트워크 패킷 송신량 계산 (KB/s)
    /// </summary>
    private void UpdateNetworkStats()
    {
        if (utp == null || statsField == null || statsType == null) return;

        try
        {
            // UTP 통계 객체 가져오기
            object stats = statsField.GetValue(utp);
            if (stats != null)
            {
                // BytesSent 또는 TotalBytesSent 필드 찾기
                FieldInfo bytesSentField = statsType.GetField("BytesSent")
                    ?? statsType.GetField("TotalBytesSent")
                    ?? statsType.GetField("PacketSent"); // 다양한 필드명 시도

                if (bytesSentField != null)
                {
                    ulong currentSentBytes = (ulong)bytesSentField.GetValue(stats);

                    // 송신 속도 계산 (bytes/s -> KB/s)
                    float deltaTime = Time.time - lastCheckTime;
                    if (deltaTime > 0 && currentSentBytes >= lastSentBytes)
                    {
                        ulong bytesDelta = currentSentBytes - lastSentBytes;
                        currentSendRate = (bytesDelta / deltaTime) / 1024f; // KB/s

                        lastSentBytes = currentSentBytes;
                        lastCheckTime = Time.time;
                    }
                }
                else
                {
                    // 필드를 못 찾았을 때 한 번만 경고 (매 프레임마다 경고하지 않도록)
                    if (lastCheckTime == 0f)
                    {
                        Debug.LogWarning($"[ClientOnlyProfiler] NetworkMetrics에서 BytesSent 필드를 찾을 수 없습니다.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ClientOnlyProfiler] 네트워크 통계 가져오기 실패: {e.Message}");
        }
    }

    /// <summary>
    /// UI 업데이트 (500ms마다)
    /// </summary>
    private void UpdateUI()
    {
        // FPS 텍스트 업데이트
        if (fpsText != null)
        {
            fpsText.text = $"FPS: {currentFPS:F0}";

            // FPS에 따라 색상 변경
            if (enableColorCoding)
            {
                if (currentFPS >= 50)
                    fpsText.color = Color.green;
                else if (currentFPS >= 30)
                    fpsText.color = Color.yellow;
                else
                    fpsText.color = Color.red;
            }
        }

        // 핑 텍스트 업데이트
        if (pingText != null)
        {
            pingText.text = $"Ping: {currentPing:F0} ms";

            // 핑에 따라 색상 변경
            if (enableColorCoding)
            {
                if (currentPing < 50)
                    pingText.color = Color.green;
                else if (currentPing < 100)
                    pingText.color = Color.yellow;
                else if (currentPing < 200)
                    pingText.color = new Color(1f, 0.5f, 0f); // 주황색
                else
                    pingText.color = Color.red;
            }
        }

        // 네트워크 텍스트 업데이트 (선택사항)
        if (networkText != null)
        {
            networkText.text = $"Send: {currentSendRate:F1} KB/s";
        }
        LogStats();
    }

    /// <summary>
    /// 현재 통계 정보를 콘솔에 출력
    /// </summary>
    public void LogStats()
    {
        Debug.Log($"[ClientOnlyProfiler] FPS: {currentFPS:F1}, Ping: {currentPing:F0}ms, Send: {currentSendRate:F1}KB/s");
    }

    /// <summary>
    /// 현재 FPS 반환
    /// </summary>
    public float GetFPS() => currentFPS;

    /// <summary>
    /// 현재 핑 반환 (ms)
    /// </summary>
    public float GetPing() => currentPing;

    /// <summary>
    /// 현재 송신 속도 반환 (KB/s)
    /// </summary>
    public float GetSendRate() => currentSendRate;

    /// <summary>
    /// 프로파일러가 초기화되었는지 확인
    /// </summary>
    public bool IsInitialized() => isInitialized;
}