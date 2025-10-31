using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : NetworkBehaviour
{
    [Header("�̵� ����")]
    [SerializeField] float moveSpeed = 5f;   // �̵� �ӵ� (m/s)

    [Header("ī�޶� ���� �̵�")]
    [SerializeField] Camera cam;             // ���� ���� ī�޶� �ڵ� �Ҵ�

    Rigidbody rb;

    Vector3 wishDir; // �Է����κ��� ����� ���ϴ� �̵� ����(���� ����)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation; // ���� ȸ�� ����(�Ѿ����� �ʰ�)
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        Move();
    }

    void FixedUpdate()
    {
        // 4) ���� �̵��� FixedUpdate����
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

        // 2) ī�޶� ���� XZ ��� ���� ��� (ī�޶� ������ ���� ����)
        Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;

        // 3) ���ϴ� �̵� ����(����ȭ)
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
