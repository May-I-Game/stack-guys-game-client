using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInputHandler : NetworkBehaviour
{
    // 모바일 UI 세팅
    private FixedJoystick joystick;

    // PlayerController가 읽어갈 값
    public Vector2 MoveInput { get; private set; }
    public bool JumpInput { get; private set; }
    public bool GrabInput { get; private set; }

    // GC 최적화: Vector2 재사용
    private Vector2 camFwdCache;
    private Vector2 camRightCache;

    private void Start()
    {
        if (!IsOwner) return;

        //생성될 때 MobileInputManager에서 참조 가져오기
        if (MobileInputManager.Instance != null)
        {
            joystick = MobileInputManager.Instance.joystick;

            //버튼 이벤트 연결 - PointerDown으로 즉시 반응
            SetupButtonPointerDown(MobileInputManager.Instance.jumpButton, OnJumpButtonPressed);
            SetupButtonPointerDown(MobileInputManager.Instance.grabButton, OnGrabButtonPressed);
        }
    }

    // PointerDown 이벤트 설정 (버튼을 누르는 순간 즉시 반응)
    private void SetupButtonPointerDown(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback.AddListener((data) => { callback(); });
        trigger.triggers.Add(entry);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (!IsOwner) return;

        // EventTrigger는 GameObject에 붙어있으므로 버튼이 파괴될 때 자동으로 정리됨
        // 별도의 수동 해제 불필요
    }

    private void Update()
    {
        if (!IsOwner) return;

        // 로비/게임 중에만 입력 받기
        if (GameManager.instance.IsLobby || GameManager.instance.IsGame)
        {
            // ============ PC: 기존 키보드 입력 ============ // WASD 입력 받기
            float horizontal = Input.GetAxisRaw("Horizontal"); // A, D
            float vertical = Input.GetAxisRaw("Vertical");     // W, S

            // ============ 모바일 : 조이스틱 입력 추가 ===========
            if (joystick != null)
            {
                horizontal += joystick.Horizontal;
                vertical += joystick.Vertical;
            }

            // ============ 카메라에 영향을 받는 이동 ===========

            // 메인 카메라 참조
            var cam = Camera.main != null ? Camera.main.transform : null;

            Vector2 dir;
            if (cam != null)
            {
                // GC 최적화: Vector2 재사용 (매 프레임 할당 방지)
                camFwdCache.x = cam.forward.x;
                camFwdCache.y = cam.forward.z;
                camFwdCache.Normalize();

                camRightCache.x = cam.right.x;
                camRightCache.y = cam.right.z;
                camRightCache.Normalize();

                // 입력(Vertical=앞/뒤, Horizontal=좌/우)을 카메라 기준으로 합성
                dir = camFwdCache * vertical + camRightCache * horizontal;
            }
            else
            {
                // 카메라가 없으면 기존 월드 기준으로 대체
                dir.x = horizontal;
                dir.y = vertical;
            }

            // 대각선 과입력(√2) 보정
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            MoveInput = dir;

            // Space 키로 점프 또는 다이브 (PC만)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                JumpInput = true;
            }

            // E 키로 잡기 또는 던지기 (PC만)
            if (Input.GetKeyDown(KeyCode.E))
            {
                GrabInput = true;
            }

            //m 키로 모바일 UI 숨기기/표시하기
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (MobileInputManager.Instance != null)
                {
                    MobileInputManager.Instance.ToggleCanvas();
                }
            }
        }
    }

    public void ResetJumpInput()
    {
        JumpInput = false;
    }

    public void ResetGrabInput()
    {
        GrabInput = false;
    }

    public void OnJumpButtonPressed()
    {
        // 로비/게임 중일 때만 즉시 입력 처리
        if (GameManager.instance != null &&
            (GameManager.instance.IsLobby || GameManager.instance.IsGame))
        {
            JumpInput = true;
        }
    }

    public void OnGrabButtonPressed()
    {
        // 로비/게임 중일 때만 즉시 입력 처리
        if (GameManager.instance != null &&
            (GameManager.instance.IsLobby || GameManager.instance.IsGame))
        {
            GrabInput = true;
        }
    }
}
