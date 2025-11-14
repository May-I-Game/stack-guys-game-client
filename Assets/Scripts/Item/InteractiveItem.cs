using Unity.Netcode;
using UnityEngine;

public enum ItemTriggerType
{
    UseOnGrab,   // 잡는 순간 바로 사용 (예: 포션, 코인)
    UseOnImpact  // 던져서 어딘가에 닿으면 사용 (예: 물폭탄, 지뢰)
}

public abstract class InteractiveItem : GrabbableObject
{
    [Header("Item Settings")]
    public ItemTriggerType triggerType;

    private bool wasThrown = false;
    private PlayerController thrower;

    public new void OnGrabbed()
    {
        base.OnGrabbed();
        wasThrown = false;
        thrower = null;

        // 잡자마자 사용하는 타입이면 바로 발동
        if (triggerType == ItemTriggerType.UseOnGrab)
        {
            ActivateItem();
        }
    }

    public new void OnThrown()
    {
        base.OnThrown();
        wasThrown = true;
        thrower = holder;
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // 던져진 상태이고 + 충돌형 아이템이라면
        if (triggerType == ItemTriggerType.UseOnImpact && wasThrown)
        {
            // 던진 사람은 영향 x
            if (thrower != null && collision.gameObject == thrower.gameObject) return;

            ActivateItem();
        }
    }

    // 실제 아이템 효과 (자식 클래스에서 구현)
    protected virtual void ActivateItem()
    {
        if (!IsSpawned) return;
        Debug.Log($"[Item] {gameObject.name} 사용됨!");
        GetComponent<NetworkObject>().Despawn();
    }
}
