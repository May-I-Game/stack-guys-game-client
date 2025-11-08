using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 서버의 전체 Tick Rate 및 프레임 사이클을 측정하는 모니터
/// Update → FixedUpdate → LateUpdate 전체 사이클을 추적
/// </summary>
public class ServerTickRateMonitor : MonoBehaviour
{
    private static ServerTickRateMonitor _instance;

    // Tick Rate 측정
    private int _fixedUpdateCount = 0;
    private int _updateCount = 0;
    private float _lastTickRateReportTime = 0f;
    private float _tickRateReportInterval = 1f; // 1초마다 측정

    // 프레임 시간 측정
    private Stopwatch _frameStopwatch = new Stopwatch();
    private Stopwatch _fixedUpdateStopwatch = new Stopwatch();
    private long _totalFrameTicks = 0;
    private long _totalFixedUpdateTicks = 0;
    private int _frameCount = 0;

    // 현재 프레임 추적
    private bool _isInFrame = false;

    // 통계
    private float _currentTickRate = 0f;
    private float _currentUpdateRate = 0f;
    private float _averageFrameTimeMs = 0f;
    private float _averageFixedUpdateTimeMs = 0f;
    private float _targetTickRate = 50f; // Unity 기본 FixedUpdate 50Hz

    // Stopwatch 주파수
    private static readonly double _ticksToMs = 1000.0 / Stopwatch.Frequency;

    [Header("Settings")]
    [Tooltip("서버에서만 활성화")]
    public bool serverOnly = true;

    [Tooltip("측정 활성화 여부")]
    public bool enableMonitoring = true;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 서버에서만 실행
        if (serverOnly && !Application.isBatchMode)
        {
            UnityEngine.Debug.Log("[ServerTickRateMonitor] 클라이언트에서는 비활성화");
            enabled = false;
            return;
        }

        if (!enableMonitoring)
        {
            enabled = false;
            return;
        }

        // FixedDeltaTime으로부터 목표 tick rate 계산
        _targetTickRate = 1f / Time.fixedDeltaTime;

        UnityEngine.Debug.Log($"[ServerTickRateMonitor] 초기화 완료. 목표 Tick Rate: {_targetTickRate:F1} Hz");
    }

    void Update()
    {
        // 프레임 시작 측정
        if (!_isInFrame)
        {
            _frameStopwatch.Restart();
            _isInFrame = true;
        }

        _updateCount++;

        // Tick Rate 리포트
        float currentTime = Time.time;
        if (currentTime - _lastTickRateReportTime >= _tickRateReportInterval)
        {
            float elapsed = currentTime - _lastTickRateReportTime;

            // Tick Rate 계산
            _currentTickRate = _fixedUpdateCount / elapsed;
            _currentUpdateRate = _updateCount / elapsed;

            // 평균 프레임 시간 계산
            if (_frameCount > 0)
            {
                _averageFrameTimeMs = (float)(_totalFrameTicks * _ticksToMs / _frameCount);
                _averageFixedUpdateTimeMs = (float)(_totalFixedUpdateTicks * _ticksToMs / _fixedUpdateCount);
            }

            // 통계 저장 (ServerPerformanceProfiler에서 사용)
            SaveStats();

            // 리셋
            _fixedUpdateCount = 0;
            _updateCount = 0;
            _totalFrameTicks = 0;
            _totalFixedUpdateTicks = 0;
            _frameCount = 0;
            _lastTickRateReportTime = currentTime;
        }
    }

    void FixedUpdate()
    {
        // FixedUpdate가 호출될 때마다 즉시 카운트
        _fixedUpdateCount++;
        _fixedUpdateStopwatch.Restart();
    }

    void LateUpdate()
    {
        // FixedUpdate 시간 측정 종료
        if (_fixedUpdateStopwatch.IsRunning)
        {
            _fixedUpdateStopwatch.Stop();
            _totalFixedUpdateTicks += _fixedUpdateStopwatch.ElapsedTicks;
        }

        // 프레임 시간 측정 종료
        if (_isInFrame && _frameStopwatch.IsRunning)
        {
            _frameStopwatch.Stop();
            _totalFrameTicks += _frameStopwatch.ElapsedTicks;
            _frameCount++;
            _isInFrame = false;
        }
    }

    private void SaveStats()
    {
        // ServerPerformanceProfiler에 수동으로 기록
        // (Start/End 패턴이 아닌 직접 기록)

        // 디버그 로그로 출력
        float tickRatePerformance = (_currentTickRate / _targetTickRate) * 100f;

        string report = $"\n" +
            $"=== Server Tick Rate Report ===\n" +
            $"Target Tick Rate: {_targetTickRate:F1} Hz\n" +
            $"Actual Tick Rate: {_currentTickRate:F2} Hz ({tickRatePerformance:F1}%)\n" +
            $"Update Rate: {_currentUpdateRate:F2} Hz\n" +
            $"Avg Frame Time: {_averageFrameTimeMs:F3} ms\n" +
            $"Avg FixedUpdate Time: {_averageFixedUpdateTimeMs:F3} ms\n" +
            $"Performance: {(tickRatePerformance >= 95f ? "GOOD" : tickRatePerformance >= 80f ? "WARNING" : "CRITICAL")}\n" +
            $"================================";

        UnityEngine.Debug.Log(report);
    }

    // 외부에서 현재 통계를 가져올 수 있는 정적 메서드
    public static float GetCurrentTickRate() => _instance != null ? _instance._currentTickRate : 0f;
    public static float GetTargetTickRate() => _instance != null ? _instance._targetTickRate : 0f;
    public static float GetAverageFrameTimeMs() => _instance != null ? _instance._averageFrameTimeMs : 0f;
    public static float GetAverageFixedUpdateTimeMs() => _instance != null ? _instance._averageFixedUpdateTimeMs : 0f;
    public static float GetTickRatePerformance() => _instance != null ? (_instance._currentTickRate / _instance._targetTickRate) * 100f : 0f;
}
