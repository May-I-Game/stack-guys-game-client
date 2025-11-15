using UnityEngine;

public interface IGrabbable
{
    bool IsGrabbed { get; }
    PlayerController Holder { get; }
    GameObject GameObj { get; }
    Rigidbody Rb { get; }
    ulong NetId { get; }

    void OnGrabbed(PlayerController player);
    void OnThrown();
    void OnReleased();
}
