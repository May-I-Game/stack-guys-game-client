using System.Collections;
using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [Tooltip("이 텔포의 도착 위치 (다른 오브젝트의 Transform)")]
    public Transform destination;

    [Tooltip("텔포 후 대기 시간 (중복 방지용)")]
    public float teleportCooldown = 1f;

    private bool canTeleport = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && canTeleport)
        {
            StartCoroutine(TeleportPlayer(other));
        }
    }

    private IEnumerator TeleportPlayer(Collider player)
    {
        canTeleport = false;

        // 이동 스크립트 비활성화
        var controller = player.GetComponent<CharacterController>();
        var thirdPerson = player.GetComponent<StarterAssets.ThirdPersonController>();

        if (thirdPerson != null) thirdPerson.enabled = false;
        if (controller != null) controller.enabled = false;

        // Rigidbody 속도 초기화
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        // 실제 위치 이동
        player.transform.position = destination.position;
        player.transform.rotation = destination.rotation;

        // 잠깐 대기 (프레임 갱신 대기)
        yield return null;

        // 컨트롤러 다시 켜기
        if (controller != null) controller.enabled = true;
        if (thirdPerson != null) thirdPerson.enabled = true;

        // 중복 방지 처리
        Teleporter destTeleporter = destination.GetComponent<Teleporter>();
        if (destTeleporter != null)
            destTeleporter.canTeleport = false;

        yield return new WaitForSeconds(teleportCooldown);

        canTeleport = true;
        if (destTeleporter != null)
            destTeleporter.canTeleport = true;
    }
}
