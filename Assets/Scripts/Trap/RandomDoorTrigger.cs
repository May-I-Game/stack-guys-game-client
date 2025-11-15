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

        if (doorObjects == null || doorObjects.Length == 0)
        {
            Debug.LogError("[RandomDoorTrigger] doorObjects 배열이 비어있습니다!");
            return;
        }

        // 서버에서 랜덤 결정
        int randomIndex = Random.Range(0, doorObjects.Length);
        selectedDoorIndex.Value = randomIndex;
        // Debug.Log($"[RandomDoorTrigger] 서버가 문 선택: {randomIndex} / {doorObjects.Length}개 중");
    }

    private void OnSelectedDoorChanged(int previousValue, int newValue)
    {
        ApplyTriggerToDoor(newValue);
    }

    private void ApplyTriggerToDoor(int doorIndex)
    {
        // Debug.Log($"[RandomDoorTrigger] ApplyTriggerToDoor 호출: doorIndex={doorIndex}, 총 문 개수={doorObjects.Length}");

        // 모든 문의 DoubleDoorTrigger 컴포넌트 비활성화
        foreach (GameObject door in doorObjects)
        {
            if (door != null && door.TryGetComponent<DoubleDoorTrigger>(out var doorTrigger))
            {
                doorTrigger.enabled = false;
                // Debug.Log($"[RandomDoorTrigger] {door.name} DoubleDoorTrigger 비활성화");
            }
            else
            {
                Debug.LogWarning($"[RandomDoorTrigger] {door?.name ?? "null"} - DoubleDoorTrigger 컴포넌트 없음");
            }
        }

        // 선택된 문만 DoubleDoorTrigger 컴포넌트 활성화
        if (doorIndex >= 0 && doorIndex < doorObjects.Length)
        {
            GameObject selectedDoor = doorObjects[doorIndex];
            if (selectedDoor != null && selectedDoor.TryGetComponent<DoubleDoorTrigger>(out var selectedTrigger))
            {
                selectedTrigger.enabled = true;
                // Debug.Log($"[RandomDoorTrigger] ✓ {selectedDoor.name} DoubleDoorTrigger 활성화 완료!");
            }
            else
            {
                Debug.LogError($"[RandomDoorTrigger] ✗ 선택된 문 {selectedDoor?.name ?? "null"}에 DoubleDoorTrigger 없음!");
            }
        }
        else
        {
            Debug.LogError($"[RandomDoorTrigger] ✗ 잘못된 doorIndex: {doorIndex} (범위: 0-{doorObjects.Length - 1})");
        }
    }
}