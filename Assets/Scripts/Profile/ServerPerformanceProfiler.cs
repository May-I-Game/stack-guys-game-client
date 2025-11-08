using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Headless 서버에서 로직별 성능을 측정하고 로그로 출력하는 프로파일러
/// 사용법:
/// ServerPerformanceProfiler.Start("PlayerMove");
/// // ... 측정할 코드 ...
/// ServerPerformanceProfiler.End("PlayerMove");
/// </summary>
public static class ServerPerformanceProfiler
{
    private class ProfileData
    {
        public long totalTicks = 0;
        public int callCount = 0;
        public long minTicks = long.MaxValue;
        public long maxTicks = 0;
        public Stopwatch activeStopwatch = null;
    }

    private static Dictionary<string, ProfileData> profiles = new Dictionary<string, ProfileData>();
    private static bool isEnabled = true;
    private static float logInterval = 5f; // 5초마다 로그 출력
    private static float lastLogTime = 0f;
    private static string logFilePath = "";

    // Stopwatch 주파수 (틱 → 밀리초 변환용)
    private static readonly double ticksToMs = 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// 프로파일러 초기화
    /// </summary>
    public static void Initialize(bool enabled = true, float logIntervalSeconds = 5f, string logPath = "")
    {
        isEnabled = enabled;
        logInterval = logIntervalSeconds;
        logFilePath = logPath;

        if (string.IsNullOrEmpty(logFilePath))
        {
            logFilePath = Path.Combine(Application.persistentDataPath, "server_performance.log");
        }

        if (isEnabled)
        {
            UnityEngine.Debug.Log($"[ServerProfiler] 초기화 완료. 로그 간격: {logInterval}초, 파일: {logFilePath}");
        }
    }

    /// <summary>
    /// 측정 시작
    /// </summary>
    public static void Start(string name)
    {
        if (!isEnabled) return;

        if (!profiles.ContainsKey(name))
        {
            profiles[name] = new ProfileData();
        }

        var data = profiles[name];

        // 이미 실행 중인 경우 경고
        if (data.activeStopwatch != null)
        {
            UnityEngine.Debug.LogWarning($"[ServerProfiler] '{name}' 이미 측정 중입니다. End()를 호출하지 않았습니다.");
            return;
        }

        data.activeStopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// 측정 종료
    /// </summary>
    public static void End(string name)
    {
        if (!isEnabled) return;

        if (!profiles.ContainsKey(name))
        {
            UnityEngine.Debug.LogWarning($"[ServerProfiler] '{name}' 측정이 시작되지 않았습니다.");
            return;
        }

        var data = profiles[name];

        if (data.activeStopwatch == null)
        {
            UnityEngine.Debug.LogWarning($"[ServerProfiler] '{name}' 측정이 시작되지 않았습니다.");
            return;
        }

        data.activeStopwatch.Stop();
        long elapsed = data.activeStopwatch.ElapsedTicks;

        data.totalTicks += elapsed;
        data.callCount++;
        data.minTicks = Math.Min(data.minTicks, elapsed);
        data.maxTicks = Math.Max(data.maxTicks, elapsed);
        data.activeStopwatch = null;
    }

    /// <summary>
    /// 측정 데이터 로그 출력 (주기적으로 호출)
    /// </summary>
    public static void LogStats()
    {
        if (!isEnabled) return;
        if (profiles.Count == 0) return;

        float currentTime = Time.time;
        if (currentTime - lastLogTime < logInterval)
        {
            return;
        }

        lastLogTime = currentTime;

        // 평균 시간 기준으로 정렬
        var sortedProfiles = profiles
            .Where(kvp => kvp.Value.callCount > 0)
            .OrderByDescending(kvp => kvp.Value.totalTicks)
            .ToList();

        if (sortedProfiles.Count == 0) return;

        string header = "=== Server Performance Report ===";
        string footer = "================================";

        List<string> logLines = new List<string> { header };
        logLines.Add($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        logLines.Add($"Interval: {logInterval}초");
        logLines.Add("");

        // Tick Rate 정보 추가
        float tickRate = ServerTickRateMonitor.GetCurrentTickRate();
        float targetTickRate = ServerTickRateMonitor.GetTargetTickRate();
        float frameTimeMs = ServerTickRateMonitor.GetAverageFrameTimeMs();
        float fixedUpdateTimeMs = ServerTickRateMonitor.GetAverageFixedUpdateTimeMs();
        float performance = ServerTickRateMonitor.GetTickRatePerformance();

        if (tickRate > 0)
        {
            string perfStatus = performance >= 95f ? "GOOD" : performance >= 80f ? "WARNING" : "CRITICAL";
            logLines.Add("--- Tick Rate Summary ---");
            logLines.Add($"Target Tick Rate:        {targetTickRate:F1} Hz");
            logLines.Add($"Actual Tick Rate:        {tickRate:F2} Hz ({performance:F1}%) [{perfStatus}]");
            logLines.Add($"Avg Frame Time:          {frameTimeMs:F3} ms");
            logLines.Add($"Avg FixedUpdate Time:    {fixedUpdateTimeMs:F3} ms");
            logLines.Add("");
        }

        logLines.Add(string.Format("{0,-30} {1,10} {2,12} {3,12} {4,12} {5,12}",
            "Name", "Calls", "Total(ms)", "Avg(ms)", "Min(ms)", "Max(ms)"));
        logLines.Add(new string('-', 100));

        foreach (var kvp in sortedProfiles)
        {
            string name = kvp.Key;
            var data = kvp.Value;

            double totalMs = data.totalTicks * ticksToMs;
            double avgMs = totalMs / data.callCount;
            double minMs = data.minTicks * ticksToMs;
            double maxMs = data.maxTicks * ticksToMs;

            string line = string.Format("{0,-30} {1,10} {2,12:F3} {3,12:F3} {4,12:F3} {5,12:F3}",
                name, data.callCount, totalMs, avgMs, minMs, maxMs);

            logLines.Add(line);
        }

        logLines.Add(footer);

        // 콘솔 출력
        string fullLog = string.Join("\n", logLines);
        UnityEngine.Debug.Log(fullLog);

        // 파일 출력
        // try
        // {
        //     File.AppendAllText(logFilePath, fullLog + "\n\n");
        // }
        // catch (Exception e)
        // {
        //     UnityEngine.Debug.LogError($"[ServerProfiler] 로그 파일 쓰기 실패: {e.Message}");
        // }

        // 데이터 초기화 (다음 측정을 위해)
        ResetStats();
    }

    /// <summary>
    /// 통계 초기화
    /// </summary>
    public static void ResetStats()
    {
        foreach (var data in profiles.Values)
        {
            data.totalTicks = 0;
            data.callCount = 0;
            data.minTicks = long.MaxValue;
            data.maxTicks = 0;
            // activeStopwatch는 유지 (진행 중인 측정)
        }
    }

    /// <summary>
    /// 모든 데이터 클리어
    /// </summary>
    public static void Clear()
    {
        profiles.Clear();
        lastLogTime = 0f;
    }

    /// <summary>
    /// 현재 통계를 즉시 반환 (디버깅용)
    /// </summary>
    public static string GetCurrentStats()
    {
        if (profiles.Count == 0) return "No data";

        var sortedProfiles = profiles
            .Where(kvp => kvp.Value.callCount > 0)
            .OrderByDescending(kvp => kvp.Value.totalTicks)
            .ToList();

        List<string> lines = new List<string>();
        foreach (var kvp in sortedProfiles)
        {
            var data = kvp.Value;
            double avgMs = (data.totalTicks * ticksToMs) / data.callCount;
            lines.Add($"{kvp.Key}: {data.callCount} calls, {avgMs:F3}ms avg");
        }

        return string.Join("\n", lines);
    }
}
