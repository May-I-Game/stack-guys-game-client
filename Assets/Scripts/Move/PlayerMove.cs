// CapsuleMover.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CapsuleMover : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] float moveSpeed = 5f;   // 이동 속도 (m/s)

    [Header("카메라 기준 이동")]
    [SerializeField] Camera cam;             // 비우면 메인 카메라 자동 할당

    Rigidbody rb;
    Vector3 wishDir; // 입력으로부터 계산한 원하는 이동 방향(월드 기준)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 물리 회전 고정(넘어지지 않게)
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        // 1) 입력 받기
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        // 2) 카메라 기준 XZ 평면 방향 계산 (카메라가 없으면 월드 기준)
        Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;

        // 3) 원하는 이동 방향(정규화)
        Vector3 dir = (right * h + forward * v);
        wishDir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;

        // (선택) 이동 방향을 바라보게 회전
        if (wishDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.2f);
        }
    }

    void FixedUpdate()
    {
        // 4) 물리 이동은 FixedUpdate에서
        Vector3 targetPos = rb.position + wishDir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPos);
    }
}
