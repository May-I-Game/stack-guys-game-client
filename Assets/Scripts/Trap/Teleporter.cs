using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
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
        if (NetworkManager.Singleton.IsServer)
        {
            if (other.CompareTag("Player") && canTeleport)
            {
                StartCoroutine(TeleportPlayer(other));
            }
        }
    }

    private IEnumerator TeleportPlayer(Collider player)
    {
        canTeleport = false;

        // 이동 스크립트 비활성화
        var controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        // Rigidbody 속도 초기화
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // PlayerController 입력 상태 초기화 (텔레포트 전 입력이 남아있는 버그)
        if (controller != null)
        {
            controller.ResetStateServerRpc();
        }

        // 실제 위치 이동
        // 네트워크 객체의 Transform 을 직접 변경시 미끄러지는 현상 발생함(보간에 의해)
        // NetworkTransform 의 Teleport 메서드를 사용하여 위치를 갱신해야함
        NetworkTransform nt = player.GetComponent<NetworkTransform>();
        if (nt != null)
        {
            nt.Teleport(destination.position, destination.rotation, player.transform.localScale);
        }

        // 잠깐 대기 (프레임 갱신 대기)
        yield return null;

        // 컨트롤러 다시 켜기
        if (controller != null) controller.enabled = true;

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
