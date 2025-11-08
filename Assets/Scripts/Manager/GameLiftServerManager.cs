#if UNITY_SERVER
using Aws.GameLift.Server;
using Amazon.GameLift.Model;            // 필요 시 유지
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GameLiftServerManager : MonoBehaviour
{
    public static GameLiftServerManager Instance { get; private set; }

    private int _gamePort = 7779; // 기본값
    private bool _sdkInited = false;
    private bool _sessionActive = false;
    private readonly Dictionary<ulong, string> _clientToPlayerSessionId = new Dictionary<ulong, string>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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
        _sdkInited = true;

        // NetworkManager 이벤트 구독 (플레이어 퇴장 감지)
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // 2) GameLift에 프로세스 준비 알리기 + health/terminate 콜백
        var processParams = new ProcessParameters(
            onStartGameSession: (gameSession) =>
            {
                _sessionActive = true;
                _clientToPlayerSessionId.Clear();

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

    // ✅ 추가: 성능 영향 방지 — 이벤트 구독 해제(중복 콜백 방지) 및 인스턴스 정리
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // 플레이어 세션 수락 (NetworkGameManager에서 호출)
    public bool AcceptPlayerSession(ulong clientId, string playerSessionId)
    {
        if (!_sdkInited || !_sessionActive || string.IsNullOrEmpty(playerSessionId))
        {
            return false;
        }

        var result = GameLiftServerAPI.AcceptPlayerSession(playerSessionId);
        if (result.Success)
        {
            _clientToPlayerSessionId[clientId] = playerSessionId;
            Debug.Log($"[GameLift] Player session accepted: {playerSessionId}");
            return true;
        }
        else
        {
            Debug.LogError($"[GameLift] AcceptPlayerSession failed: {result.Error}");
            return false;
        }
    }

    // 플레이어 퇴장 처리
    private void OnClientDisconnected(ulong clientId)
    {
        if (!_sdkInited || !_clientToPlayerSessionId.TryGetValue(clientId, out string playerSessionId))
        {
            return;
        }

        var result = GameLiftServerAPI.RemovePlayerSession(playerSessionId);
        if (result.Success)
        {
            Debug.Log($"[GameLift] Player session removed: {playerSessionId}");
        }
        else
        {
            Debug.LogError($"[GameLift] RemovePlayerSession failed: {result.Error}");
        }

        _clientToPlayerSessionId.Remove(clientId);

        // ✅ 활성 플레이어가 0명이면 세션 종료 (_sessionActive일 때만)
        if (_sessionActive && _clientToPlayerSessionId.Count == 0)
        {
            Debug.Log("[GameLift] No active players - terminating game session");
            GameLiftServerAPI.ProcessEnding();
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
