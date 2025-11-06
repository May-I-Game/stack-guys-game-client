using UnityEngine;
using Unity.Netcode;

public class RandomDoorTrigger : NetworkBehaviour
{
    [Header("Door Settings")]
    [Tooltip("Door_Double 프리팹들을 직접 드래그해서 넣으세요")]
    public GameObject[] doorObjects;

    // 서버에서 결정한 랜덤 인덱스를 모든 클라이언트와 동기화
    private NetworkVariable<int> selectedDoorIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 서버에서만 랜덤 선택
        if (IsServer)
        {
            SelectRandomDoor();
        }

        // 값이 변경될 때마다 호출되는 콜백 등록
        selectedDoorIndex.OnValueChanged += OnSelectedDoorChanged;

        // 이미 값이 설정되어 있다면 (늦게 접속한 클라이언트)
        if (selectedDoorIndex.Value != -1)
        {
            ApplyTriggerToDoor(selectedDoorIndex.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        selectedDoorIndex.OnValueChanged -= OnSelectedDoorChanged;
        base.OnNetworkDespawn();
    }

    private void SelectRandomDoor()
    {
        if (!IsServer) return;

        // 서버에서 랜덤 결정
        int randomIndex = Random.Range(0, doorObjects.Length);
        selectedDoorIndex.Value = randomIndex;
    }

    private void OnSelectedDoorChanged(int previousValue, int newValue)
    {
        ApplyTriggerToDoor(newValue);
    }

    private void ApplyTriggerToDoor(int doorIndex)
    {
        // 먼저 모든 문의 Collider를 isTrigger = false로 초기화
        foreach (GameObject door in doorObjects)
        {
            if (door != null)
            {
                Collider[] allColliders = door.GetComponentsInChildren<Collider>(true);
                foreach (Collider col in allColliders)
                {
                    col.isTrigger = false;
                }
            }
        }

        // 선택된 문만 isTrigger = true로 설정
        GameObject selectedDoor = doorObjects[doorIndex];

        Collider[] selectedColliders = selectedDoor.GetComponentsInChildren<Collider>(true);

        foreach (Collider col in selectedColliders)
        {
            col.enabled = true;
        }
    }
}