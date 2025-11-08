using Unity.Netcode;
using UnityEngine;

public class PlayerBody : NetworkBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ocean"))
        {
            this.gameObject.GetComponent<NetworkObject>().Despawn(destroy: false);
        }
    }
}
