using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput; // 선택사항: 이름 입력

    public void OnClickStart()
    {
        // 이름 저장 (선택사항)
        var name = (nameInput?.text ?? "").Trim();
        if (!string.IsNullOrEmpty(name))
        {
            PlayerPrefs.SetString("player_name", name);
        }
        else
        {
            // 이름이 없으면 기본값 사용
            PlayerPrefs.SetString("player_name", "Player_" + Random.Range(1000, 9999));
        }
        PlayerPrefs.Save();

        // 서버 연결
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        string serverAddress = transport.ConnectionData.Address;
        ushort serverPort = transport.ConnectionData.Port;

        Debug.Log($"Connecting to {serverAddress}:{serverPort}...");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartClient();
        }
        else
        {
            Debug.LogError("NetworkManager not found!");
        }
    }
}