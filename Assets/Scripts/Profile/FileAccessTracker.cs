using UnityEngine;
using System.IO;

/// <summary>
/// FileAccessTracker - 파일 읽기/쓰기 자동 추적 헬퍼
/// 
/// [목적]
/// - 게임에서 발생하는 모든 파일 읽기/쓰기를 자동으로 추적
/// - CompleteNGOProfiler와 연동하여 CSV 로그에 기록
/// 
/// [사용 방법]
/// 1. 게임 시작 시 FileAccessTracker.Initialize() 호출
/// 2. 기존 File.ReadAllText() → FileAccessTracker.ReadAllText()로 변경
/// 3. 기존 File.WriteAllText() → FileAccessTracker.WriteAllText()로 변경
/// 
/// [지원 함수]
/// - ReadAllText, ReadAllBytes, ReadAllLines
/// - WriteAllText, WriteAllBytes, AppendAllText
/// 
/// [주의사항]
/// - Initialize()를 반드시 먼저 호출해야 함
/// - CompleteNGOProfiler가 씬에 있어야 함
/// - 모든 파일 액세스 코드를 수정해야 정확한 통계 가능
/// 
/// </summary>
public static class FileAccessTracker
{
    // 내부 변수
    /// <summary>
    /// CompleteNGOProfiler 참조 (파일 액세스 횟수 기록용)
    /// </summary>
    private static CompleteNGOProfiler profiler;

    // 초기화

    /// <summary>
    /// FileAccessTracker 초기화
    /// - CompleteNGOProfiler를 찾아서 캐싱
    /// - 게임 시작 시 한 번만 호출하면 됨
    /// 
    /// 사용 예시:
    /// void Start() {
    ///     FileAccessTracker.Initialize();
    /// }
    /// </summary>
    public static void Initialize()
    {
        // 씬에서 CompleteNGOProfiler 컴포넌트 찾기
        profiler = Object.FindObjectOfType<CompleteNGOProfiler>();

        if (profiler == null)
        {
            Debug.LogWarning("[FileAccessTracker] CompleteNGOProfiler를 찾을 수 없습니다! 파일 액세스 추적이 비활성화됩니다.");
        }
        else
        {
            Debug.Log("[FileAccessTracker] 초기화 완료! 파일 액세스 추적 시작.");
        }
    }

    // 파일 읽기 함수 (자동 추적 포함)

    /// <summary>
    /// 파일을 문자열로 읽기 (자동 추적)
    /// - 파일 읽기 전에 프로파일러에 카운트 증가
    /// - 실제로는 File.ReadAllText() 호출
    /// 
    /// 사용 예시:
    /// string json = FileAccessTracker.ReadAllText("save.json");
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <returns>파일 내용 (문자열)</returns>
    public static string ReadAllText(string path)
    {
        // 프로파일러에 읽기 이벤트 기록
        profiler?.LogFileRead();

        // 실제 파일 읽기
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 파일을 바이트 배열로 읽기 (자동 추적)
    /// - 이미지, 바이너리 파일 등에 사용
    /// 
    /// 사용 예시:
    /// byte[] data = FileAccessTracker.ReadAllBytes("texture.png");
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <returns>파일 내용 (바이트 배열)</returns>
    public static byte[] ReadAllBytes(string path)
    {
        // 프로파일러에 읽기 이벤트 기록
        profiler?.LogFileRead();

        // 실제 파일 읽기
        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// 파일을 줄 단위로 읽기 (자동 추적)
    /// - 설정 파일, 로그 파일 등에 사용
    /// 
    /// 사용 예시:
    /// string[] lines = FileAccessTracker.ReadAllLines("config.txt");
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <returns>파일 내용 (문자열 배열, 각 줄)</returns>
    public static string[] ReadAllLines(string path)
    {
        // 프로파일러에 읽기 이벤트 기록
        profiler?.LogFileRead();

        // 실제 파일 읽기
        return File.ReadAllLines(path);
    }

    // 파일 쓰기 함수 (자동 추적 포함)
    /// <summary>
    /// 문자열을 파일로 쓰기 (자동 추적)
    /// - 기존 파일이 있으면 덮어쓰기
    /// - JSON, 텍스트 저장에 사용
    /// 
    /// 사용 예시:
    /// FileAccessTracker.WriteAllText("save.json", json);
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <param name="contents">저장할 내용</param>
    public static void WriteAllText(string path, string contents)
    {
        // 프로파일러에 쓰기 이벤트 기록
        profiler?.LogFileWrite();

        // 실제 파일 쓰기
        File.WriteAllText(path, contents);
    }

    /// <summary>
    /// 바이트 배열을 파일로 쓰기 (자동 추적)
    /// - 이미지, 바이너리 파일 저장에 사용
    /// 
    /// 사용 예시:
    /// FileAccessTracker.WriteAllBytes("screenshot.png", bytes);
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <param name="bytes">저장할 바이트 배열</param>
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        // 프로파일러에 쓰기 이벤트 기록
        profiler?.LogFileWrite();

        // 실제 파일 쓰기
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// 문자열을 파일 끝에 추가 (자동 추적)
    /// - 기존 파일 내용 유지하고 끝에 추가
    /// - 로그 파일 작성에 유용
    /// 
    /// 사용 예시:
    /// FileAccessTracker.AppendAllText("log.txt", "새 로그\n");
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <param name="contents">추가할 내용</param>
    public static void AppendAllText(string path, string contents)
    {
        // 프로파일러에 쓰기 이벤트 기록
        profiler?.LogFileWrite();

        // 실제 파일 추가
        File.AppendAllText(path, contents);
    }
}

// 사용 예시 클래스

/// <summary>
/// FileAccessTracker 사용 예시
/// - 실제 게임 코드에서 이렇게 사용하면 됨
/// </summary>
public class FileAccessExample : MonoBehaviour
{
    // 초기화
    /// <summary>
    /// 게임 시작 시 FileAccessTracker 초기화
    /// - 한 번만 호출하면 됨 (보통 GameManager에서)
    /// </summary>
    void Start()
    {
        // FileAccessTracker 초기화
        FileAccessTracker.Initialize();

        Debug.Log("[FileAccessExample] FileAccessTracker 초기화 완료!");
    }

    // 잘못된 사용 예시 (추적 안 됨)
    /// <summary>
    /// 잘못된 방식: File 클래스 직접 사용
    /// - 프로파일러에 기록되지 않음!
    /// </summary>
    void BadExample()
    {
        // 이렇게 하면 추적 안 됨
        string data = File.ReadAllText("data.json");
        File.WriteAllText("save.json", data);
    }

    // 올바른 사용 예시 (자동 추적)
    /// <summary>
    /// 올바른 방식: FileAccessTracker 사용
    /// - 자동으로 프로파일러에 기록됨!
    /// </summary>
    void GoodExample()
    {
        // 이렇게 하면 자동 추적됨
        string data = FileAccessTracker.ReadAllText("data.json");
        FileAccessTracker.WriteAllText("save.json", data);
    }

    // 실전 사용 예시 1: 게임 세이브/로드
    /// <summary>
    /// 게임 데이터 저장
    /// - JSON 직렬화 후 파일로 저장
    /// - 자동으로 프로파일러에 "쓰기 1회" 기록
    /// </summary>
    public void SaveGame()
    {
        // 게임 데이터 생성
        GameData data = new GameData
        {
            level = 5,
            score = 1000.5f
        };

        // JSON으로 직렬화
        string json = JsonUtility.ToJson(data);

        // 파일 경로
        string path = Path.Combine(Application.persistentDataPath, "save.json");

        // 저장 (자동으로 프로파일러에 기록됨!)
        FileAccessTracker.WriteAllText(path, json);

        Debug.Log($"게임 저장 완료: {path}");
    }

    /// <summary>
    /// 게임 데이터 로드
    /// - 파일에서 JSON 읽기
    /// - 자동으로 프로파일러에 "읽기 1회" 기록
    /// </summary>
    public GameData LoadGame()
    {
        string path = Path.Combine(Application.persistentDataPath, "save.json");

        // 파일 존재 확인
        if (!File.Exists(path))
        {
            Debug.LogWarning("세이브 파일 없음!");
            return null;
        }

        // 로드 (자동으로 프로파일러에 기록됨!)
        string json = FileAccessTracker.ReadAllText(path);

        // JSON 역직렬화
        GameData data = JsonUtility.FromJson<GameData>(json);

        Debug.Log($"게임 로드 완료: Level {data.level}, Score {data.score}");
        return data;
    }

    // 실전 사용 예시 2: 설정 파일 관리
    /// <summary>
    /// 설정 저장
    /// - 키-값 형태로 저장
    /// </summary>
    public void SaveSettings()
    {
        string path = Path.Combine(Application.persistentDataPath, "settings.txt");

        // 설정 문자열 생성
        string settings = "volume=0.8\n" +
                         "quality=high\n" +
                         "fullscreen=true";

        // 저장 (자동 추적)
        FileAccessTracker.WriteAllText(path, settings);

        Debug.Log("설정 저장 완료!");
    }

    /// <summary>
    /// 설정 로드
    /// - 줄 단위로 읽기
    /// </summary>
    public void LoadSettings()
    {
        string path = Path.Combine(Application.persistentDataPath, "settings.txt");

        if (!File.Exists(path)) return;

        // 줄 단위로 로드 (자동 추적)
        string[] lines = FileAccessTracker.ReadAllLines(path);

        foreach (string line in lines)
        {
            Debug.Log($"설정: {line}");
        }
    }

    // 실전 사용 예시 3: 로그 파일 작성
    /// <summary>
    /// 커스텀 로그 파일에 기록
    /// - 매번 파일 끝에 추가
    /// </summary>
    public void WriteLog(string message)
    {
        string path = Path.Combine(Application.persistentDataPath, "game.log");

        // 타임스탬프 포함
        string log = $"[{System.DateTime.Now:HH:mm:ss}] {message}\n";

        // 추가 (자동 추적)
        FileAccessTracker.AppendAllText(path, log);
    }

    // 실전 사용 예시 4: 스크린샷 저장
    /// <summary>
    /// 스크린샷 저장
    /// - 바이트 배열로 저장
    /// </summary>
    public void SaveScreenshot()
    {
        // 스크린샷 캡처 (가상 예시)
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();

        // PNG로 인코딩
        byte[] bytes = screenshot.EncodeToPNG();

        // 파일 경로
        string filename = $"screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = Path.Combine(Application.persistentDataPath, filename);

        // 저장 (자동 추적)
        FileAccessTracker.WriteAllBytes(path, bytes);

        Debug.Log($"스크린샷 저장: {path}");

        // 메모리 정리
        Destroy(screenshot);
    }
}

// 데이터 클래스
/// <summary>
/// 게임 데이터 예시 클래스
/// </summary>
[System.Serializable]
public class GameData
{
    public int level;
    public float score;
    public string playerName;
}