using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System; // Action �̺�Ʈ ���

public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Joystick Components")]
    public RectTransform background;
    public RectTransform handle;
    public Button jumpButton; // Jump ��ư (RectTransform�� �ƴ� Button���� ����)

    [Header("Settings")]
    public float handleRange = 50f;
    public float deadZone = 0.1f;

    private Vector2 inputVector;

    // �ܺο��� ������ �� �ִ� ���� �̺�Ʈ
    public event Action OnJumpPressed;

    private void Start()
    {
        // ���� ��ư�� �����Ǿ� ������ Ŭ�� �� �̺�Ʈ ȣ��
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

        // ����ȭ
        position = Vector2.ClampMagnitude(position, handleRange);
        handle.anchoredPosition = position;

        // �Է� ���� ��� (-1 ~ 1 ����)
        inputVector = position / handleRange;

        // ������ ����
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
