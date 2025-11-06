using UnityEngine;

public class DummyController : PlayerController
{
    [Header("Bot Settings")]
    public float rotateIntervalMin = 1f;
    public float rotateIntervalMax = 1.5f;
    private float rotateTimer = 3f;
    private Vector2 currentMoveDir = Vector2.zero;
    public float jumpIntervalMin = 3f;
    public float jumpIntervalMax = 5f;
    private float jumpTimer = 3f;

    protected override void Update()
    {
        if (IsOwner)
        {
            // 입력 허용시만 요청 처리
            if (inputEnabled)
            {
                BotAI();
            }
        }

        UpdateAnimation();
    }

    private void BotAI()
    {
        rotateTimer -= Time.deltaTime;
        if (rotateTimer < 0)
        {
            MovePlayerServerRpc(currentMoveDir);
            currentMoveDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            rotateTimer = Random.Range(rotateIntervalMin, rotateIntervalMax);
        }

        jumpTimer -= Time.deltaTime;
        if (jumpTimer < 0)
        {
            JumpPlayerServerRpc();
            jumpTimer = Random.Range(jumpIntervalMin, jumpIntervalMax);
        }
    }
}