using Unity.Netcode;
using UnityEngine;

public class PlayerInputHandler : NetworkBehaviour
{
    // 모바일 UI 세팅
    private FixedJoystick joystick;
    private bool jumpButtonPressed = false;
    private bool grabButtonPressed = false;

    // PlayerController가 읽어갈 값
    public Vector2 MoveInput { get; private set; }
    public bool JumpInput { get; private set; }
    public bool GrabInput { get; private set; }

    private void Start()
    {
        if (!IsOwner) return;

        //생성될 때 MobileInputManager에서 참조 가져오기
        if (MobileInputManager.Instance != null)
        {
            joystick = MobileInputManager.Instance.joystick;

            //버튼 이벤트 연결
            if (MobileInputManager.Instance.jumpButton != null)
            {
                MobileInputManager.Instance.jumpButton.onClick.AddListener(OnJumpButtonPressed);
            }
            if (MobileInputManager.Instance.grabButton != null)
            {
                MobileInputManager.Instance.grabButton.onClick.AddListener(OnGrabButtonPressed);
            }

        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (!IsOwner) return;

        // 파괴될 때 이벤트 해제 (메모리 누수 방지)
        if (MobileInputManager.Instance != null)
        {
            if (MobileInputManager.Instance.jumpButton != null)
            {
                MobileInputManager.Instance.jumpButton.onClick.RemoveListener(OnJumpButtonPressed);
            }

            if (MobileInputManager.Instance.grabButton != null)
            {
                MobileInputManager.Instance.grabButton.onClick.RemoveListener(OnGrabButtonPressed);
            }
        }
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
                // 카메라의 forward/right를 수평면(Y=0)에 투영해 기저벡터 생성
                Vector2 camFwd = new Vector2(cam.forward.x, cam.forward.z).normalized;
                Vector2 camRight = new Vector2(cam.right.x, cam.right.z).normalized;

                // 입력(Vertical=앞/뒤, Horizontal=좌/우)을 카메라 기준으로 합성
                dir = camFwd * vertical + camRight * horizontal;
            }
            else
            {
                // 카메라가 없으면 기존 월드 기준으로 대체
                dir = new Vector2(horizontal, vertical);
            }

            // 대각선 과입력(√2) 보정
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            MoveInput = dir;

            // Space 키로 점프 또는 다이브
            if (Input.GetKeyDown(KeyCode.Space) || jumpButtonPressed)
            {
                JumpInput = true;
                jumpButtonPressed = false;
            }

            // E 키로 잡기 또는 던지기
            if (Input.GetKeyDown(KeyCode.E) || grabButtonPressed)
            {
                GrabInput = true;
                grabButtonPressed = false;
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
        jumpButtonPressed = true;
    }

    public void OnGrabButtonPressed()
    {
        grabButtonPressed = true;
    }
}
