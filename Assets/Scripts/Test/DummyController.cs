using UnityEngine;

public class DummyController : PlayerController
{
    [Header("Bot Settings")]
    public float actionIntervalMin = 3f;
    public float actionIntervalMax = 5f;
    private Vector3 currentMoveDir = Vector3.zero;
    private float rotateTimer = 3f;
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
            currentMoveDir = new Vector3(Random.Range(0f, 1f), 0, Random.Range(0f, 1f));
            rotateTimer = Random.Range(actionIntervalMin, actionIntervalMax);
        }
        MovePlayerServerRpc(currentMoveDir);

        jumpTimer -= Time.deltaTime;
        if (jumpTimer < 0)
        {
            JumpPlayerServerRpc();
            jumpTimer = Random.Range(actionIntervalMin, actionIntervalMax);
        }
    }
}