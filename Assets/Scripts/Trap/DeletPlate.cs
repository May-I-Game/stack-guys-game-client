using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class DeletPlate : NetworkBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private GameObject wall;

    [Header("Button Visual")]
    [SerializeField] private Transform buttonTransform; // 눌릴 버튼 오브젝트
    [SerializeField] private float pressDepth = 0.1f;   // 버튼이 눌리는 깊이
    [SerializeField] private float buttonSpeed = 10f;   // 버튼 애니메이션 속도

    private int objectsOnPlate = 0;
    private bool isPressed = false;

    private bool hasActivated = false; // 이미 한번 눌려서 작동했는지

    private Vector3 originalPosition;  // 버튼의 원래 위치
    private Vector3 targetPosition;    // 버튼의 목표 위치

    // 벽 활성화 상태를 네트워크로 동기화 (서버만 쓰기 가능)
    private NetworkVariable<bool> isWallActive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        // 버튼의 원래 위치 저장
        if (buttonTransform != null)
        {
            originalPosition = buttonTransform.localPosition;
            targetPosition = originalPosition;
        }
    }

    private void Update()
    {
        // 버튼을 부드럽게 목표 위치로 이동
        if (buttonTransform != null)
        {
            buttonTransform.localPosition = Vector3.Lerp(
                buttonTransform.localPosition,
                targetPosition,
                Time.deltaTime * buttonSpeed
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 초기 벽 상태 적용
        UpdateWallState(isWallActive.Value);

        // NetworkVariable 값 변경 감지 리스너 등록
        isWallActive.OnValueChanged += OnWallStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        // 리스너 해제
        isWallActive.OnValueChanged -= OnWallStateChanged;
        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.GetComponent<Rigidbody>() != null)
        {
            // 이미 작동했다면 무시
            if (hasActivated) return;

            hasActivated = true;
            isPressed = true;
            Debug.Log("버튼 눌림! (1회성)");

            // 버튼 내려가기
            if (buttonTransform != null)
            {
                targetPosition = originalPosition + Vector3.down * pressDepth;
            }

            // 벽 비활성화 (한 번만 실행)
            isWallActive.Value = false;
        }
    }

    // NetworkVariable 값이 변경되면 모든 클라이언트에서 호출됨
    private void OnWallStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[PressButton] 벽 상태 변경: {oldValue} -> {newValue}");
        UpdateWallState(newValue);
    }

    // 벽의 활성화 상태 업데이트
    private void UpdateWallState(bool active)
    {
        if (wall != null)
        {
            wall.SetActive(active);
            Debug.Log($"[PressButton] 벽 {(active ? "활성화" : "비활성화")}");
        }
        else
        {
            Debug.LogWarning("[PressButton] Wall GameObject가 할당되지 않았습니다!");
        }
    }
}
