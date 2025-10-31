using Unity.Netcode;
using UnityEngine;

// 테스트: 유니코드(서명없는 UTF-8) 65001

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : NetworkBehaviour
{
    [SerializeField] float moveSpeed = 5f;

    [SerializeField] Camera cam;

    Rigidbody rb;

    Vector3 wishDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (IsOwner)
        {
            Move();
        }
    }

    void FixedUpdate()
    {
        if (IsServer)
        {
            Vector3 targetPos = rb.position + wishDir * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPos);

            if (wishDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.2f);
            }
        }
    }

    private void Move()
    {
        // 1) �Է� �ޱ�
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;

        Vector3 dir = (right * h + forward * v);
        Vector3 targetPos = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero;
        MoveServerRpc(targetPos);
    }

    [ServerRpc]
    private void MoveServerRpc(Vector3 targetPos)
    {
        wishDir = targetPos;
    }
}
