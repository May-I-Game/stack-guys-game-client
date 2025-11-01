using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class LoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput; // 선택사항: 이름 입력
    [SerializeField] private Camera characterSelectCamera;
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

        // 선택한 캐릭터 인덱스 가져오기
        int selectedCharacterIndex = PlayerPrefs.GetInt("selected_character", 0);

        // 서버 연결
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        string serverAddress = transport.ConnectionData.Address;
        ushort serverPort = transport.ConnectionData.Port;

        Debug.Log($"Connecting to {serverAddress}:{serverPort}...");

        if (NetworkManager.Singleton != null)
        {
            // ConnectionData에 캐릭터 인덱스 포함
            byte[] payload = new byte[1];
            payload[0] = (byte)selectedCharacterIndex;
            NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;

            NetworkManager.Singleton.StartClient();
        }
        else
        {
            Debug.LogError("NetworkManager not found!");
        }
    }
}