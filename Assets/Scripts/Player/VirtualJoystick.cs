using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System; // Action 이벤트 사용

public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Joystick Components")]
    public RectTransform background;
    public RectTransform handle;
    public Button jumpButton; // Jump 버튼 (RectTransform이 아닌 Button으로 변경)

    [Header("Settings")]
    public float handleRange = 50f;
    public float deadZone = 0.1f;

    private Vector2 inputVector;

    // 외부에서 구독할 수 있는 점프 이벤트
    public event Action OnJumpPressed;

    private void Start()
    {
        // 점프 버튼이 설정되어 있으면 클릭 시 이벤트 호출
        if (jumpButton != null)
        {
            jumpButton.onClick.AddListener(() =>
            {
                OnJumpPressed?.Invoke();
            });
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out position
        );

        // 정규화
        position = Vector2.ClampMagnitude(position, handleRange);
        handle.anchoredPosition = position;

        // 입력 벡터 계산 (-1 ~ 1 범위)
        inputVector = position / handleRange;

        // 데드존 적용
        if (inputVector.magnitude < deadZone)
        {
            inputVector = Vector2.zero;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }

    public Vector2 GetInputVector()
    {
        return inputVector;
    }
}
