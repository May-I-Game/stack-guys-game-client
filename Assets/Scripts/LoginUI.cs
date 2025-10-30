using UnityEngine;
using TMPro;

public class LoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput; // 선택사항: 이름 입력

    [Header("Connection Settings")]
    private const string SERVER_ADDRESS = "127.0.0.1";
    private const ushort SERVER_PORT = 7779;

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
        if (NetworkGameManager.Instance != null)
        {
            Debug.Log($"Connecting to {SERVER_ADDRESS}:{SERVER_PORT}...");
            NetworkGameManager.Instance.StartClient(SERVER_ADDRESS, SERVER_PORT);
        }
        else
        {
            Debug.LogError("NetworkGameManager not found!");
        }
    }
}