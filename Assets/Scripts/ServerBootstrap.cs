using UnityEngine;

public class ServerBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (IsServerBuild())
        {
            Debug.Log("Running as Dedicated Server - Starting server...");
            StartServer();
        }
    }

    private bool IsServerBuild()
    {
        // Dedicated Server 빌드 감지
#if UNITY_SERVER
        return true;
#else
        return false;
#endif

    }

    private void StartServer()
    {
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.StartServer();
        }
        else
        {
            Debug.LogError("NetworkGameManager not found!");
        }
    }
}