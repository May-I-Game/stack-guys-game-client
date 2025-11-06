using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;

public class BotLauncher
{
    // --- 여기에 빌드된 .exe 파일의 전체 경로를 입력하세요 ---
    private const string BuildPath = @"C:\Users\서정\Documents\GitHub\stack-guys\Build\Windows_Bot\stack-guys-bot-client.exe";

    // --- 실행할 봇의 개수 ---
    private const int BotCount = 5;

    // --- 봇 실행 사이의 딜레이 (밀리초 단위, 500 = 0.5초) ---
    private const int LaunchDelayMs = 500;

    [MenuItem("Tools/Run Bots")]
    private static async Task RunBots()
    {
        // 빌드 경로가 비어있는지 확인
        if (string.IsNullOrEmpty(BuildPath))
        {
            UnityEngine.Debug.LogError("BotLauncher: BuildPath가 설정되지 않았습니다. 스크립트를 수정해주세요.");
            return;
        }

        UnityEngine.Debug.Log($"--- {BotCount}개의 봇 클라이언트를 실행합니다 ---");

        for (int i = 1; i <= BotCount; i++)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = BuildPath; // 실행 파일 경로

            // 헤드리스 모드 인수 + 로그 파일 이름 지정
            startInfo.Arguments = $"-batchmode -nographics -logFile log_bot_{i}.txt";

            // 빌드 폴더를 작업 디렉토리로 설정
            startInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(BuildPath);

            Process.Start(startInfo);

            UnityEngine.Debug.Log($"봇 {i} 실행 완료. (log_bot_{i}.txt)");

            await Task.Delay(LaunchDelayMs);
        }
    }

    [MenuItem("Tools/Kill Bots")]
    private static void KillAllBots()
    {
        if (string.IsNullOrEmpty(BuildPath))
        {
            UnityEngine.Debug.LogError("BotLauncher: BuildPath가 설정되지 않았습니다.");
            return;
        }

        // 1. 경로에서 파일 이름만 추출 (예: "MyClient")
        string processName = Path.GetFileNameWithoutExtension(BuildPath);

        // 2. 해당 이름을 가진 모든 프로세스를 찾기
        Process[] processes = Process.GetProcessesByName(processName);

        if (processes.Length == 0)
        {
            UnityEngine.Debug.Log($"실행 중인 봇({processName})이 없습니다.");
            return;
        }

        UnityEngine.Debug.Log($"--- {processes.Length}개의 봇({processName})을 종료합니다. ---");

        // 3. 찾은 모든 프로세스를 강제 종료
        foreach (Process process in processes)
        {
            try
            {
                process.Kill();
                process.WaitForExit(); // 종료될 때까지 잠시 대기
                UnityEngine.Debug.Log($"프로세스 {process.Id} 종료 완료.");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"프로세스 {process.Id} 종료 실패: {e.Message}");
            }
        }
    }
}