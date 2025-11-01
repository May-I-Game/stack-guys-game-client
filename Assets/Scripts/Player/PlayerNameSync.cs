using System.Runtime.CompilerServices;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerNameSync : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameText;

    //자동 동기화
    private NetworkVariable<FixedString64Bytes> playerName =
        new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"[PlayerNameSync] OnNetworkSpawn - Name: {playerName.Value}");

        UpdateNameDisplay(playerName.Value.ToString());

        //값 변경 감지
        playerName.OnValueChanged += OnNameChanged;
    }
    //서버만 호출
    public void SetPlayerName(string name)
    {
        if (!IsServer) return;

        playerName.Value = name;
        Debug.Log($"[Server] PlayerName NetworkVariable set to: {name}");
    }
    private void UpdateNameDisplay(string name)
    {
        if (!IsClient) return;

        if (nameText != null)
        {
            nameText.text = name;
            Debug.Log($"[Client] Name displayed: {name}");
        }
        else
        {
            Debug.LogError("[Client] NameText is null!");
        }
    }
    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        base.OnNetworkDespawn();
    }
    private void OnNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        Debug.Log($"[Client] name changed: {oldName} -> {newName}");
        UpdateNameDisplay(newName.ToString());
    }
}
