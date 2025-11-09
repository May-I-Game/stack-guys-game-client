using UnityEngine;

public class DoubleDoorTrigger : MonoBehaviour
{
    [Header("Door References")]
    public Transform leftDoor;   // Door_DoubleLeft
    public Transform rightDoor;  // Door_DoubleRight

    [Header("Settings")]
    public float openSpeed = 3f;
    public float leftOpenAngle = -90f;
    public float rightOpenAngle = 90f;

    private bool hasOpened = false;
    private bool isAnimating = false;
    private Vector3 leftInitialRotation;
    private Vector3 rightInitialRotation;
    private Vector3 leftTargetRotation;
    private Vector3 rightTargetRotation;

    [Header("NavMesh Controller")]
    public DoorNavObstacle doorNavObstacle;

    void Start()
    {
        // 초기 회전값 저장 (Euler Angles)
        leftInitialRotation = leftDoor.localEulerAngles;
        rightInitialRotation = rightDoor.localEulerAngles;

        if (doorNavObstacle == null)
        {
            doorNavObstacle = GetComponent<DoorNavObstacle>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!hasOpened)
        {
            hasOpened = true;
            isAnimating = true;

            // Y축만 변경한 목표 회전값
            leftTargetRotation = new Vector3(
                leftInitialRotation.x,
                leftInitialRotation.y + leftOpenAngle,
                leftInitialRotation.z
            );

            rightTargetRotation = new Vector3(
                rightInitialRotation.x,
                rightInitialRotation.y + rightOpenAngle,
                rightInitialRotation.z
            );
        }

        // door의 Obstacle 제어 : 문 열기
        if (doorNavObstacle != null)
        {
            doorNavObstacle.OpenDoor();
        }
    }

    void Update()
    {
        if (isAnimating)
        {
            // EulerAngles로 부드럽게 회전
            leftDoor.localEulerAngles = new Vector3(
                leftDoor.localEulerAngles.x,
                Mathf.LerpAngle(leftDoor.localEulerAngles.y, leftTargetRotation.y, Time.deltaTime * openSpeed),
                leftDoor.localEulerAngles.z
            );

            rightDoor.localEulerAngles = new Vector3(
                rightDoor.localEulerAngles.x,
                Mathf.LerpAngle(rightDoor.localEulerAngles.y, rightTargetRotation.y, Time.deltaTime * openSpeed),
                rightDoor.localEulerAngles.z
            );

            // 거의 다 열렸으면 애니메이션 중지
            if (Mathf.Abs(Mathf.DeltaAngle(leftDoor.localEulerAngles.y, leftTargetRotation.y)) < 0.1f)
            {
                leftDoor.localEulerAngles = leftTargetRotation;
                rightDoor.localEulerAngles = rightTargetRotation;
                isAnimating = false;
            }
        }
    }
}