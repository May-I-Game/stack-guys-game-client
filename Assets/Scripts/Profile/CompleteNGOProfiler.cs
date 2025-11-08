using System;
using System.Reflection;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
public class CompleteNGOProfiler : NetworkBehaviour
{
    [Header("추적 항목")]
    [Tooltip("네트워크 송신량, 핑 추적")]

    [SerializeField] private TMP_Text Ping;
    [SerializeField] private TMP_Text Fps;

    private float ping = 0;

    // 새 RTT 측정 변수 (RPC 기반)
    private double lastPingSendTime;
    private float pingRpcTimer = 0f;
    private const float pingRpcInterval = 0.3f; // RPC 핑 측정 주기

    // UI 업데이트 타이머
    private float uiUpdateTimer = 0f;
    private const float uiUpdateInterval = 0.5f; // UI 업데이트 주기 (500ms)

    // 성능 통계 변수
    private float fps = 0;
    private float minFPS = 999;
    private float maxFPS = 0;
    private float frameTime = 0;

    // NGO 네트워크 관련 변수
    private NetworkManager nm;
    private UnityTransport utp;
    private FieldInfo statsField;
    private Type statsType;

    private System.Collections.Generic.List<float> pingHistory = new System.Collections.Generic.List<float>(30);
    private const int PING_HISTORY_COUNT = 30; // 30 프레임(또는 0.5초) 평균

    // Unity 생명주기 - 초기화
    /// <summary>
    /// 컴포넌트 시작 시 초기화
    /// - NetworkManager 확인
    /// - UTP Transport 초기화 (리플렉션으로 통계 접근)
    /// - 이벤트 구독
    /// - 로그 파일 생성
    /// </summary>
    void Start()
    {
        // NetworkManager 싱글톤 가져오기
        nm = NetworkManager.Singleton;
        if (nm == null)
        {
            UnityEngine.Debug.LogError("[CompleteNGOProfiler] NetworkManager가 없습니다!");
            enabled = false;
            return;
        }
        if (!IsClient)
        {
            UnityEngine.Debug.LogError("[CompleteNGOProfiler] 클라이언트가 아니므로 비활성화합니다!");
            enabled = false;
            return;
        }

        // UTP Transport 초기화 (네트워크 통계 접근용)
        utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp != null)
        {
            try
            {
                // 리플렉션으로 UTP 내부 통계 필드 접근
                statsField = typeof(UnityTransport).GetField("m_Statistics", BindingFlags.NonPublic | BindingFlags.Instance);
                if (statsField != null)
                {
                    statsType = statsField.FieldType;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CompleteNGOProfiler] UTP 리플렉션 오류: {e.Message}");
            }
        }
    }

    // Unity 생명주기 - 업데이트
    /// <summary>
    /// 매 프레임 호출
    /// - 단축키 처리 (F6, F7, F8)
    /// - 통계 업데이트 (FPS, 메모리, 네트워크 등)
    /// - 주기적 로깅 (logInterval마다)
    /// </summary>
    void Update()
    {
        // NetworkManager가 없거나 네트워크가 시작 안 됐으면 리턴
        if (nm == null || !nm.IsListening) return;


        // 통계 업데이트
        UpdateStats();

        // ✅ Ping RPC 주기적으로 실행
        pingRpcTimer += Time.deltaTime;
        if (pingRpcTimer >= pingRpcInterval)
        {
            pingRpcTimer = 0f;
            lastPingSendTime = NetworkManager.LocalTime.Time;
            PingServerRpc(lastPingSendTime);
        }

        // UI 업데이트 (500ms마다)
        uiUpdateTimer += Time.deltaTime;
        if (uiUpdateTimer >= uiUpdateInterval)
        {
            uiUpdateTimer = 0f;
            ShowUI();
        }
    }

    // ✅ Ping RPC (round-trip)
    [Rpc(SendTo.Server)]
    private void PingServerRpc(double timestamp)
    {
        PongClientRpc(timestamp);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PongClientRpc(double timestamp)
    {
        double now = NetworkManager.LocalTime.Time;
    }

    // 통계 업데이트 함수
    /// <summary>
    /// 모든 통계 데이터 업데이트
    /// </summary>
    private void UpdateStats()
    {
        // ===== 네트워크: 핑 측정 (클라이언트만) =====
        try
        {
            ping = utp.GetCurrentRtt(0);
        }
        catch { /* ... */ }

        // RTT 값을 이동 평균으로 필터링하여 UI에 표시
        if (pingHistory.Count >= PING_HISTORY_COUNT)
        {
            pingHistory.RemoveAt(0); // 가장 오래된 값 제거
        }
        pingHistory.Add(ping); // 새 값 추가

        float totalPing = 0;
        foreach (float p in pingHistory)
        {
            totalPing += p;
        }

        // 현재 FPS 계산
        float currentFPS = 1f / Time.unscaledDeltaTime;
        fps = currentFPS;

        // 최소/최대 FPS 갱신
        minFPS = Mathf.Min(minFPS, currentFPS);
        maxFPS = Mathf.Max(maxFPS, currentFPS);

        // 프레임 타임 (ms)
        frameTime = Time.deltaTime * 1000f;
    }

    // Unity GUI - 디버그 UI (스크롤 기능 추가됨)
    /// <summary>
    /// 화면에 디버그 UI 표시
    /// - showDebugUI가 true일 때만 표시
    /// - 스크롤 뷰를 사용하여 내용이 많아도 스크롤 가능
    /// </summary>
    private void ShowUI()
    {
        Ping.text = $"Ping: {ping:F0} ms";
        Fps.text = $"FPS: {fps:F1}";
    }
}