using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// CompleteNGOProfiler - Unity Netcode for GameObjects (NGO) 전용 프로파일러
/// 
/// [추적 항목]
/// 1. 네트워크: 패킷 송신량, 송신 속도, 핑(RTT)
/// 2. 성능: FPS, 프레임 타임, CPU 부하율
/// 3. 메모리: 사용량, Mono Heap, GC 발생 횟수
/// 4. 렌더링: Batches, Triangles, Vertices, SetPassCalls (에디터에서만 정확)
/// 5. Physics: FixedUpdate 시간 (물리 시뮬레이션)
/// 6. 파일 액세스: 파일 읽기/쓰기 횟수 (FileAccessTracker 사용 필요)
/// 
/// [사용 방법]
/// 1. NetworkManager GameObject에 이 컴포넌트 추가
/// 2. Inspector에서 추적 항목 체크
/// 3. 게임 실행 시 자동 로깅 시작 (Auto Start On Connect)
/// 4. 수동 제어: F6=시작, F7=중지, F8=즉시 스냅샷
/// 
/// [로그 파일]
/// - 위치: Application.persistentDataPath/ngo_[role]_[timestamp].csv
/// - 형식: CSV (Excel로 열기 가능)
/// - 업데이트: logInterval 초마다 자동 기록
/// 
/// [중요]
/// - 렌더링 통계는 Unity Editor에서만 정확함 (빌드에서는 0)
/// - 파일 액세스는 FileAccessTracker 사용 시에만 추적됨
/// - WebGL에서는 IndexedDB에 저장됨
/// 
/// </summary>
public class CompleteNGOProfiler : NetworkBehaviour
{
    // 설정 변수
    [Header("설정")]
    [Tooltip("로그 기록 주기 (초 단위, 기본 1초)")]
    [SerializeField] private float logInterval = 1f;

    [Tooltip("연결 시 자동으로 로깅 시작")]
    [SerializeField] private bool autoStartOnConnect = true;

    [Tooltip("화면에 디버그 UI 표시")]
    [SerializeField] private bool showDebugUI = true;

    [Header("추적 항목")]
    [Tooltip("네트워크 송신량, 핑 추적")]
    [SerializeField] private bool trackNetwork = true;

    [Tooltip("FPS, 프레임 타임, CPU 부하 추적")]
    [SerializeField] private bool trackPerformance = true;

    [Tooltip("메모리 사용량, GC 추적")]
    [SerializeField] private bool trackMemory = true;

    [Tooltip("렌더링 통계 추적 (에디터에서만 정확)")]
    [SerializeField] private bool trackRendering = true;

    [Tooltip("Physics 시뮬레이션 시간 추적")]
    [SerializeField] private bool trackPhysics = true;

    [Tooltip("파일 읽기/쓰기 횟수 추적 (FileAccessTracker 필요)")]
    [SerializeField] private bool trackFileAccess = true;

    [Tooltip("로그를 저장할 고정된 절대 경로. 비어있으면 Application.persistentDataPath를 사용합니다.")]
    [SerializeField] private string fixedLogDirectory = "C:\\Users\\user\\Documents\\GitHub\\stack-guys\\Build\\Profiller";

    // 내부 변수 - 로깅 시스템

    private string logFilePath;                    // CSV 파일 경로
    private StringBuilder sb = new StringBuilder(2000);  // CSV 문자열 빌더
    private bool isLogging = false;                // 현재 로깅 중인지 여부
    private float nextLogTime = 0;                 // 다음 로그 기록 시간

    // 네트워크 통계 변수
    private ulong lastSentBytes = 0;               // 이전 측정 시점의 총 송신 바이트
    private ulong lastSentPackets = 0;             // 이전 측정 시점의 총 송신 패킷
    private float sentRate = 0;                    // 초당 송신 속도 (bytes/s)
    private float ping = 0;                        // 서버-클라이언트 간 왕복 시간 (ms)

    // 성능 통계 변수
    private float fps = 0;                         // 현재 FPS
    private float minFPS = 999;                    // 최소 FPS (리셋 시 999로 초기화)
    private float maxFPS = 0;                      // 최대 FPS
    private float frameTime = 0;                   // 프레임 시간 (ms)

    // 메모리 통계 변수
    private long usedMemoryMB = 0;                 // 사용 중인 메모리 (MB)
    private long monoMemoryMB = 0;                 // Mono Heap 메모리 (MB)
    private int gcCount = 0;                       // Garbage Collection 발생 횟수
    private int lastGCCount = 0;                   // 이전 GC 카운트 (증가 감지용)

    // 렌더링 통계 변수 (Unity Editor에서만 정확)
    private int batches = 0;                       // Draw Call 배치 수
    private int triangles = 0;                     // 렌더링된 삼각형 수
    private int vertices = 0;                      // 렌더링된 버텍스 수
    private int setPassCalls = 0;                  // SetPass Call 수

    // Physics 통계 변수
    private Stopwatch physicsStopwatch = new Stopwatch();  // Physics 시간 측정용
    private float physicsTimeMs = 0;               // FixedUpdate 실행 시간 (ms)

    // 파일 액세스 통계 변수
    private int fileReadCount = 0;                 // 파일 읽기 횟수
    private int fileWriteCount = 0;                // 파일 쓰기 횟수

    // CPU 부하 변수
    private float cpuLoadPercent = 0;              // CPU 부하율 (60 FPS 기준 %)

    // NGO 네트워크 관련 변수
    private NetworkManager nm;                     // NetworkManager 싱글톤 캐시
    private UnityTransport utp;                    // UTP Transport 캐시
    private FieldInfo statsField;                  // UTP 통계 필드 (리플렉션)
    private Type statsType;                        // UTP 통계 타입 (리플렉션)

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

        // UTP Transport 초기화 (네트워크 통계 접근용)
        utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
        if (utp != null && trackNetwork)
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

        // 네트워크 이벤트 구독
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnServerStarted += OnServerStarted;

        // 로그 파일 초기화
        InitializeLog();
    }

    /// <summary>
    /// 컴포넌트 파괴 시 이벤트 구독 해제
    /// </summary>
    new void OnDestroy()
    {
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnServerStarted -= OnServerStarted;
        }
    }

    // 네트워크 이벤트 핸들러
    /// <summary>
    /// 서버 시작 시 호출
    /// - autoStartOnConnect가 true면 자동 로깅 시작
    /// </summary>
    private void OnServerStarted()
    {
        if (autoStartOnConnect) StartLogging();
    }

    /// <summary>
    /// 클라이언트 연결 시 호출
    /// - 순수 클라이언트이고 autoStartOnConnect가 true면 자동 로깅 시작
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        if (IsClient && !IsServer && autoStartOnConnect) StartLogging();
    }

    // 로그 파일 초기화
    /// <summary>
    /// 로그 파일 경로 설정 및 CSV 헤더 작성
    /// - 파일명 형식: ngo_[role]_[timestamp].csv
    /// - role: server, client, host
    /// - 저장 위치: Application.persistentDataPath
    /// </summary>
    private void InitializeLog()
    {
        // ... (역할, 타임스탬프 생략)
        string role = "..."; // 역할
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); // 타임스탬프

        // 1. 기본 저장 경로 설정 (fixedLogDirectory가 설정되어 있으면 그것을 사용)
        string baseDirectory;
        if (!string.IsNullOrEmpty(fixedLogDirectory))
        {
            // 고정 경로 사용: Inspector에 입력한 경로가 baseDirectory가 됩니다.
            baseDirectory = fixedLogDirectory;
        }
        else
        {
            // 안전 경로 사용: Inspector가 비어있으면 유니티 기본 저장 경로 사용
            baseDirectory = Application.persistentDataPath;
        }

        // 2. 디렉토리가 없으면 생성
        try
        {
            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[CompleteNGOProfiler] 디렉토리 생성 실패 ({baseDirectory}): {e.Message}. PersistentDataPath를 대신 사용합니다.");
            // 실패 시 안전 경로로 대체
            baseDirectory = Application.persistentDataPath;
            if (!Directory.Exists(baseDirectory)) Directory.CreateDirectory(baseDirectory);
        }

        // 3. 로그 파일 경로 생성 (올바른 경로 결합)
        logFilePath = Path.Combine(baseDirectory, $"ngo_{role}_{timestamp}.csv");

        // ... (WriteHeader() 호출 등 생략)
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

        // 단축키 처리
        if (Input.GetKeyDown(KeyCode.F6)) StartLogging();   // 로깅 시작
        if (Input.GetKeyDown(KeyCode.F7)) StopLogging();    // 로깅 중지
        if (Input.GetKeyDown(KeyCode.F8)) TakeSnapshot();   // 즉시 스냅샷

        // 통계 업데이트
        UpdateStats();

        // 로깅 중이고 다음 로그 시간이 됐으면 스냅샷
        if (isLogging && Time.time >= nextLogTime)
        {
            TakeSnapshot();
            nextLogTime = Time.time + logInterval;
        }
    }

    void FixedUpdate()
    {
        if (trackPhysics)
        {
            physicsStopwatch.Restart();
        }
    }

    void LateUpdate()
    {
        if (trackPhysics && physicsStopwatch.IsRunning)
        {
            physicsStopwatch.Stop();
            physicsTimeMs = (float)physicsStopwatch.Elapsed.TotalMilliseconds;
        }
    }

    // 통계 업데이트 함수
    /// <summary>
    /// 모든 통계 데이터 업데이트
    /// - 네트워크: 핑 측정
    /// - 성능: FPS, 프레임 타임, CPU 부하 계산
    /// - 메모리: 사용량, GC 카운트
    /// - 렌더링: Batches, Triangles 등 (에디터에서만)
    /// </summary>
    private void UpdateStats()
    {
        // ===== 네트워크: 핑 측정 (클라이언트만) =====
        if (trackNetwork && utp != null && !nm.IsServer)
        {
            try
            {
                // UTP의 GetCurrentRtt로 서버와의 왕복 시간 측정
                ping = utp.GetCurrentRtt(nm.LocalClientId);
            }
            catch
            {
                ping = 0;  // 실패 시 0
            }
        }

        // ===== 성능: FPS 및 CPU 부하 =====
        if (trackPerformance)
        {
            // 현재 FPS 계산
            float currentFPS = 1f / Time.unscaledDeltaTime;
            fps = currentFPS;

            // 최소/최대 FPS 갱신
            minFPS = Mathf.Min(minFPS, currentFPS);
            maxFPS = Mathf.Max(maxFPS, currentFPS);

            // 프레임 타임 (ms)
            frameTime = Time.deltaTime * 1000f;

            // CPU 부하율 계산 (60 FPS를 100%로 가정)
            float targetFrameTime = 1f / 60f;
            cpuLoadPercent = Mathf.Clamp01(Time.deltaTime / targetFrameTime) * 100f;
        }

        // ===== 메모리: 사용량 및 GC =====
        if (trackMemory)
        {
            // 총 할당된 메모리 (MB)
            usedMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / 1048576;

            // Mono Heap 메모리 (MB)
            monoMemoryMB = Profiler.GetMonoUsedSizeLong() / 1048576;

            // GC 발생 횟수 추적 (Generation 0 기준)
            int currentGCCount = GC.CollectionCount(0);
            if (currentGCCount > lastGCCount)
            {
                gcCount++;
                lastGCCount = currentGCCount;
            }
        }

        // ===== 렌더링: 통계 (Unity Editor에서만 정확) =====
        if (trackRendering)
        {
#if UNITY_EDITOR
            // 에디터에서는 UnityStats API 사용 가능
            batches = UnityEditor.UnityStats.batches;
            triangles = UnityEditor.UnityStats.triangles;
            vertices = UnityEditor.UnityStats.vertices;
            setPassCalls = UnityEditor.UnityStats.setPassCalls;
#else
            // 빌드에서는 통계 접근 불가 (0으로 설정)
            batches = 0;
            triangles = 0;
            vertices = 0;
            setPassCalls = 0;
#endif
        }
    }

    // 로깅 제어 함수
    /// <summary>
    /// 로깅 시작
    /// - isLogging을 true로 설정
    /// - FPS 통계 리셋
    /// - 네트워크 통계 리셋
    /// </summary>
    public void StartLogging()
    {
        isLogging = true;
        nextLogTime = Time.time;

        // 통계 리셋
        minFPS = 999;
        maxFPS = 0;
        lastSentBytes = 0;
        lastSentPackets = 0;

        UnityEngine.Debug.Log("[CompleteNGOProfiler] 로깅 시작!");
    }

    /// <summary>
    /// 로깅 중지
    /// - isLogging을 false로 설정
    /// - 파일 경로 출력
    /// </summary>
    public void StopLogging()
    {
        isLogging = false;
        UnityEngine.Debug.Log($"[CompleteNGOProfiler] 로깅 중지! 파일: {logFilePath}");
    }

    // CSV 파일 작성 함수

    /// <summary>
    /// CSV 헤더 작성
    /// - 활성화된 추적 항목에 따라 헤더 컬럼 생성
    /// - 파일 초기화 (기존 파일 덮어쓰기)
    /// </summary>
    private void WriteHeader()
    {
        sb.Clear();

        // 기본 정보
        sb.Append("TS,Role,ClientID,");

        // 네트워크 헤더
        if (trackNetwork)
            sb.Append("NET(Send,TotalMB,Ping,Pkts),");

        // 성능 헤더
        if (trackPerformance)
            sb.Append("PERF(FPS,Min,Max,FT(ms),CPU(%)),");

        // 메모리 헤더
        if (trackMemory)
            sb.Append("MEM(Used,Mono,GC),");

        // 렌더링 헤더
        if (trackRendering)
            sb.Append("RENDER(Batches,Tris,Verts,SetPass),");

        // Physics 헤더
        if (trackPhysics)
            sb.Append("PHYS(Time(ms)),");

        // 파일 액세스 헤더
        if (trackFileAccess)
            sb.Append("FILE(Reads,Writes),");

        sb.AppendLine();

        // 파일에 쓰기 (덮어쓰기)
        try
        {
            File.WriteAllText(logFilePath, sb.ToString());
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[CompleteNGOProfiler] 헤더 저장 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 현재 통계의 스냅샷을 CSV 파일에 기록
    /// - 네트워크 통계 가져오기 (UTP 리플렉션)
    /// - 송신 속도 계산
    /// - CSV 한 줄 작성
    /// - 파일에 추가
    /// </summary>
    private void TakeSnapshot()
    {
        sb.Clear();

        // ===== 네트워크 통계 가져오기 (UTP 리플렉션) =====
        ulong currentSentBytes = 0;
        ulong currentSentPackets = 0;

        if (trackNetwork && utp != null && statsField != null)
        {
            try
            {
                // UTP 내부 통계 객체 가져오기
                object stats = statsField.GetValue(utp);
                if (stats != null)
                {
                    // BytesSent, PacketsSent 필드 읽기
                    currentSentBytes = (ulong)statsType.GetField("BytesSent").GetValue(stats);
                    currentSentPackets = (ulong)statsType.GetField("PacketsSent").GetValue(stats);
                }
            }
            catch
            {
                // 실패 시 0으로 유지
            }
        }

        // 송신 속도 계산 (bytes/s)
        sentRate = (currentSentBytes - lastSentBytes) / logInterval;
        ulong packetDelta = currentSentPackets - lastSentPackets;

        // 이전 값 업데이트
        lastSentBytes = currentSentBytes;
        lastSentPackets = currentSentPackets;

        // ===== CSV 데이터 작성 시작 (레이블 추가) =====

        // 기본 정보
        sb.Append($"TS:{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},");
        sb.Append($"ID:{nm.LocalClientId},");

        // 네트워크 데이터
        if (trackNetwork)
        {
            sb.Append($"Net-Send(KB/s):{sentRate / 1024:F2},");      // KB/s
            sb.Append($"Net-Total(MB):{currentSentBytes / 1048576.0:F2},"); // MB (누적 총 전송량)
            sb.Append($"Net-Ping(ms):{ping:F1},");                 // ms
            sb.Append($"Net-Pkts:{packetDelta},");                 // 패킷 수 (직전 interval 동안)
        }

        // 성능 데이터
        if (trackPerformance)
        {
            sb.Append($"Perf-FPS:{fps:F1},");
            sb.Append($"Perf-MinFPS:{minFPS:F1},");
            sb.Append($"Perf-MaxFPS:{maxFPS:F1},");
            sb.Append($"Perf-FT(ms):{frameTime:F2},");
            sb.Append($"Perf-CPU(%):{cpuLoadPercent:F1},");
        }

        // 메모리 데이터
        if (trackMemory)
        {
            sb.Append($"Mem-Used(MB):{usedMemoryMB},");
            sb.Append($"Mem-Mono(MB):{monoMemoryMB},");
            sb.Append($"Mem-GC:{gcCount},");
        }

        // 렌더링 데이터
        if (trackRendering)
        {
            sb.Append($"Render-Batch:{batches},");
            sb.Append($"Render-Tris:{triangles},");
            sb.Append($"Render-Verts:{vertices},");
            sb.Append($"Render-SPC:{setPassCalls},");
        }

        // Physics 데이터
        if (trackPhysics)
        {
            sb.Append($"Phys-Time(ms):{physicsTimeMs:F2},");
        }

        // 파일 액세스 데이터
        if (trackFileAccess)
        {
            sb.Append($"File-Reads:{fileReadCount},");
            sb.Append($"File-Writes:{fileWriteCount},");
        }

        sb.AppendLine();

        // ===== 파일에 저장 (추가 모드) =====
        try
        {
            File.AppendAllText(logFilePath, sb.ToString());
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[CompleteNGOProfiler] 저장 실패: {e.Message}");
        }
    }

    // 공개 API - 파일 액세스 추적

    /// <summary>
    /// 파일 읽기 이벤트 기록
    /// - FileAccessTracker에서 호출됨
    /// </summary>
    public void LogFileRead()
    {
        fileReadCount++;
    }

    /// <summary>
    /// 파일 쓰기 이벤트 기록
    /// - FileAccessTracker에서 호출됨
    /// </summary>
    public void LogFileWrite()
    {
        fileWriteCount++;
    }

    // Unity GUI - 디버그 UI

    /// <summary>
    /// 화면에 디버그 UI 표시
    /// - showDebugUI가 true일 때만 표시
    /// - 네트워크, 성능, 메모리, 렌더링, Physics, 파일 통계
    /// - 단축키 안내
    /// </summary>
    void OnGUI()
    {
        // UI 비활성화 또는 네트워크 미시작 시 리턴
        if (!showDebugUI || !nm.IsListening) return;

        // UI 박스 크기
        int w = 380;
        int h = 400;

        // 배경 박스
        GUI.Box(new Rect(10, 10, w, h), "");
        GUILayout.BeginArea(new Rect(15, 15, w - 10, h - 10));

        // ===== 타이틀 =====
        string statusIcon = isLogging ? "REC" : "IDLE";
        string roleIcon = IsServer ? (IsClient ? "HOST" : "SERVER") : "CLIENT";
        GUILayout.Label($"{statusIcon} | {roleIcon}");
        GUILayout.Space(5);

        // ===== 네트워크 통계 =====
        if (trackNetwork)
        {
            GUILayout.Label("━━━ 네트워크 ━━━");
            GUILayout.Label($"송신: {sentRate / 1024:F1} KB/s");
            GUILayout.Label($"총 전송: {lastSentBytes / 1048576.0:F1} MB");
            if (!IsServer)
                GUILayout.Label($"핑: {ping:F0} ms");
            GUILayout.Label($"패킷: {lastSentPackets}");
            GUILayout.Space(5);
        }

        // ===== 성능 통계 =====
        if (trackPerformance)
        {
            GUILayout.Label("━━━ 성능 ━━━");
            GUILayout.Label($"FPS: {fps:F1} (Min {minFPS:F1}, Max {maxFPS:F1})");
            GUILayout.Label($"프레임: {frameTime:F2} ms");
            GUILayout.Label($"CPU 부하: {cpuLoadPercent:F1}%");
            GUILayout.Space(5);
        }

        // ===== 메모리 통계 =====
        if (trackMemory)
        {
            GUILayout.Label("━━━ 메모리 ━━━");
            GUILayout.Label($"사용: {usedMemoryMB} MB");
            GUILayout.Label($"Mono: {monoMemoryMB} MB");
            GUILayout.Label($"GC: {gcCount}회");
            GUILayout.Space(5);
        }

        // ===== 렌더링 통계 =====
        if (trackRendering)
        {
            GUILayout.Label("━━━ 렌더링 ━━━");
#if UNITY_EDITOR
            GUILayout.Label($"Batches: {batches}");
            GUILayout.Label($"Triangles: {triangles}");
#else
            GUILayout.Label("렌더링: Editor에서만");
#endif
            GUILayout.Space(5);
        }

        // ===== Physics 통계 =====
        if (trackPhysics)
        {
            GUILayout.Label("━━━ Physics ━━━");
            GUILayout.Label($"시간: {physicsTimeMs:F2} ms");
            GUILayout.Space(5);
        }

        // ===== 파일 액세스 통계 =====
        if (trackFileAccess)
        {
            GUILayout.Label("━━━ 파일 액세스 ━━━");
            GUILayout.Label($"읽기: {fileReadCount}");
            GUILayout.Label($"쓰기: {fileWriteCount}");
        }

        // ===== 단축키 안내 =====
        GUILayout.Space(10);
        GUILayout.Label("F6: 시작 | F7: 중지 | F8: 스냅샷");

        GUILayout.EndArea();
    }
}