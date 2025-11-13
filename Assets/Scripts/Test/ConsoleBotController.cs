using Unity.Netcode;
using UnityEngine;

public class ConsoleBotController : PlayerController
{
    protected override void Update()
    {
        UpdateAnimation();
    }

    public void MoveBot(Vector2 direction)
    {
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || isDiving)
        {
            moveDir = Vector2.zero;
            return;
        }

        // Debug.Log($"[이동] 플레이어 이동 Rpc 호출됨!: {direction}");

        // 이동 방향 임계값 체크: 방향 변화가 크거나 멈출 때만 동기화
        Vector2 directionDelta = direction - moveDir;
        if (directionDelta.magnitude >= inputDeltaThreshold || direction == Vector2.zero)
        {
            moveDir = direction;
        }
    }

    public void JumpBot()
    {
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || isDiving)
        {
            return;
        }

        isJumpQueued = true;
    }
}