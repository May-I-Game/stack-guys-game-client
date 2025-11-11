using UnityEngine;
using UnityEngine.Events; // UnityEvent를 사용하기 위해 필요

public class PressButton : MonoBehaviour
{
    // 버튼이 눌렸을 때 실행될 함수를 인스펙터에서 등록할 수 있게 해줍니다.
    public UnityEvent onPressed;
    // 버튼에서 물건이 제거되었을 때 실행될 함수를 등록할 수 있습니다. (선택 사항)
    public UnityEvent onReleased;

    private int objectsOnPlate = 0;
    private bool isPressed = false;

    // 물건이 버튼 영역(트리거)에 진입했을 때 호출됩니다.
    private void OnTriggerEnter(Collider other)
    {
        // Rigidbody를 가진 오브젝트만 감지하도록 할 수 있습니다. (선택 사항)
        if (other.GetComponent<Rigidbody>() != null)
        {
            objectsOnPlate++;
            // 처음 물건이 올라갔을 때만 이벤트 실행
            if (objectsOnPlate == 1 && !isPressed)
            {
                isPressed = true;
                Debug.Log("버튼 눌림!");
                onPressed.Invoke(); // 등록된 함수 실행
                // 시각적인 변화 (예: 버튼을 살짝 아래로 내리는 코드)를 여기에 추가할 수 있습니다.
            }
        }
    }

    // 물건이 버튼 영역(트리거)에서 나갔을 때 호출됩니다.
    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<Rigidbody>() != null)
        {
            objectsOnPlate--;
            // 모든 물건이 제거되었을 때만 이벤트 실행
            if (objectsOnPlate == 0 && isPressed)
            {
                isPressed = false;
                Debug.Log("버튼 해제됨!");
                onReleased.Invoke(); // 등록된 함수 실행
                // 시각적인 변화 (예: 버튼을 원래 위치로 올리는 코드)를 여기에 추가할 수 있습니다.
            }
        }
    }
}
