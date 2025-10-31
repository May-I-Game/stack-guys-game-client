using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

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

        // 캐릭터 선택된 것도 반영되어야 함. (CharactorCamera localposition.x 를 -2로 나누기 하면 인덱스가 나옴)

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