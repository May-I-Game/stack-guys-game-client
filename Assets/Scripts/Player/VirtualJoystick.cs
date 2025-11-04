using UnityEngine;
using UnityEngine.UI;
using System;

public class VirtualJoystick : MonoBehaviour
{
    [Header("Joystick Components")]
    public RectTransform background;
    public RectTransform handle;
    public Button jumpButton;                       // Jump 버튼 (RectTransform이 아닌 Button으로 변경)

    [Header("Settings")]
    public float handleRange = 50f;
    public float deadZone = 0.1f;
    [Range(0.1f, 0.5f)]
    public float joystickScreenHeightPercent = 0.3f;    // 조이스틱 활성 영역 (화면 하단 30%)

    private Vector2 inputVector;                        // 입력 벡터 (-1 ~ 1 범위)        
    private int assignedTouchId = -1;                   // 할당된 터치 ID (멀티터치 지원)
    private Camera uiCamera;                            // UI 카메라 

    public event Action OnJumpPressed;                  // 외부에서 구독할 수 있는 점프 이벤트

    private void Start()
    {
        // UI 카메라 찾기
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            uiCamera = canvas.worldCamera;
        }

        // 점프 버튼이 설정되어 있으면 클릭 시 이벤트 호출
        if (jumpButton != null)
        {
            jumpButton.onClick.AddListener(() =>
            {
                OnJumpPressed?.Invoke();
            });
        }
    }

    private void Update()
    {
        // 터치 입력이 있으면 터치 우선 처리
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
        }
        // 터치가 없으면 마우스 입력 처리 (PC 브라우저/에디터)
        else
        {
            HandleMouseInput();
        }
    }

    // 터치 입력 처리 (멀티터치 지원)
    private void HandleTouchInput()
    {
        // 터치가 없으면 조이스틱 리셋
        if (Input.touchCount == 0 && assignedTouchId != -1)
        {
            ResetJoystick();
            return;
        }

        // 모든 터치 검사
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            // 터치 시작
            if (touch.phase == TouchPhase.Began)
            {
                // 아직 할당된 터치가 없고, 조이스틱 영역 내 터치인 경우
                if (assignedTouchId == -1 && IsTouchInJoystickArea(touch.position))
                {
                    assignedTouchId = touch.fingerId;
                    UpdateJoystickPosition(touch.position);
                }
            }
            // 터치 이동
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                // 할당된 터치만 처리 (다른 터치는 무시)
                if (touch.fingerId == assignedTouchId)
                {
                    UpdateJoystickPosition(touch.position);
                }
            }
            // 터치 종료
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (touch.fingerId == assignedTouchId)
                {
                    ResetJoystick();
                }
            }
        }
    }

    // 에디터 테스트용 마우스 입력
    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (assignedTouchId == -1 && IsTouchInJoystickArea(Input.mousePosition))
            {
                assignedTouchId = 0; // 마우스는 ID 0
                UpdateJoystickPosition(Input.mousePosition);
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if (assignedTouchId == 0)
            {
                UpdateJoystickPosition(Input.mousePosition);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (assignedTouchId == 0)
            {
                ResetJoystick();
            }
        }
    }

    // 터치 위치가 조이스틱 영역 내에 있는지 확인 (화면 하단 N% 체크)
    private bool IsTouchInJoystickArea(Vector2 screenPosition)
    {
        return screenPosition.y < Screen.height * joystickScreenHeightPercent;
    }

    // 조이스틱 핸들 위치 업데이트
    private void UpdateJoystickPosition(Vector2 screenPosition)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            screenPosition,
            uiCamera,
            out localPoint
        );

        // 정규화
        localPoint = Vector2.ClampMagnitude(localPoint, handleRange);
        handle.anchoredPosition = localPoint;

        // 입력 벡터 계산 (-1 ~ 1 범위)
        inputVector = localPoint / handleRange;

        // 데드존 적용
        if (inputVector.magnitude < deadZone)
        {
            inputVector = Vector2.zero;
        }
    }

    // 조이스틱 초기화
    private void ResetJoystick()
    {
        assignedTouchId = -1;
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }

    // 외부에서 입력 벡터 가져오기
    public Vector2 GetInputVector()
    {
        return inputVector;
    }
}