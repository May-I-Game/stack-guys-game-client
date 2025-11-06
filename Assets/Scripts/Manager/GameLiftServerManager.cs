#if UNITY_SERVER
using Aws.GameLift.Server;
using Amazon.GameLift.Model;            // 필요 시 유지
using System.Collections.Generic;
using UnityEngine;

public class GameLiftServerManager : MonoBehaviour
{
    private int _gamePort = 7779; // 기본값

    void Start()
    {
        // 1) 명령줄 인수로 포트 받기:  -port 7779  또는  -port 7780 ...
        _gamePort = GetPortFromArgs(7779);

        var init = GameLiftServerAPI.InitSDK();
        if (!init.Success)
        {
            Debug.LogError($"InitSDK Fail: {init.Error}");
            return;
        }

        // 2) GameLift에 프로세스 준비 알리기 + health/terminate 콜백
        var processParams = new ProcessParameters(
            onStartGameSession: (gameSession) =>
            {
                // 세션 활성화 전에 NGO/UTP 리슨 포트를 동적으로 세팅
                var transport = Object.FindFirstObjectByType<Unity.Netcode.Transports.UTP.UnityTransport>();
                if (transport != null)
                {
                    // 주소는 서버 리슨용: 0.0.0.0, 포트는 _gamePort
                    // (SetConnectionData(string address, ushort port, string listenAddress))
                    transport.SetConnectionData("0.0.0.0", (ushort)_gamePort, "0.0.0.0");
                }
                else
                {
                    Debug.LogError("UnityTransport not found on server scene!");
                }

                // GameLift에 세션 활성화 알림
                var activate = GameLiftServerAPI.ActivateGameSession();
                if (!activate.Success)
                {
                    Debug.LogError($"ActivateGameSession failed: {activate.Error}");
                    return;
                }

                // NGO 서버 시작
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null)
                {
                    if (!nm.IsServer && !nm.IsHost)
                    {
                        bool ok = nm.StartServer();
                        Debug.Log(ok
                            ? $"[Server] Started listening on port {_gamePort}"
                            : "[Server] StartServer failed");
                    }
                }
                else
                {
                    Debug.LogError("NetworkManager.Singleton is null");
                }
            },
            onUpdateGameSession: (update) =>
            {
                // 필요시 세션 속성 업데이트 처리 (옵션)
            },
            onProcessTerminate: () =>
            {
                // 종료 신호: 정리 후 종료
                GameLiftServerAPI.ProcessEnding();
            },
            onHealthCheck: () =>
            {
                // 헬스 체크: 외부 의존성 확인 로직을 넣을 수 있음
                return true;
            },
            port: _gamePort,  // 3) GameLift가 인지할 이 프로세스의 포트 (동일 포트 사용)
            logParameters: new LogParameters(new List<string>
            {
                "/local/game/logs/gameliftserver.log"
            })
        );

        var ready = GameLiftServerAPI.ProcessReady(processParams);
        if (ready.Success)
        {
            Debug.Log($"ProcessReady Success (port: {_gamePort})");
        }
        else
        {
            Debug.LogError($"ProcessReady Fail: {ready.Error}");
        }
    }

    private int GetPortFromArgs(int defaultPort)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-port" && int.TryParse(args[i + 1], out int p) && p > 0 && p < 65536)
            {
                Debug.Log($"[Args] Using port from args: {p}");
                return p;
            }
        }
        Debug.Log($"[Args] Using default port: {defaultPort}");
        return defaultPort;
    }

    private void OnApplicationQuit()
    {
        GameLiftServerAPI.Destroy();
    }
}
#endif  