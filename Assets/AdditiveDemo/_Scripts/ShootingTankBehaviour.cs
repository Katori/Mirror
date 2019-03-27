using UnityEngine;
using Mirror;

public class ShootingTankBehaviour : NetworkBehaviour
{
    [SyncVar]
    public Quaternion Rotation;

    private NetworkAnimator NetAnim;

    [ServerCallback]
    private void Start()
    {
        NetAnim = GetComponent<NetworkAnimator>();
    }

    private void Update()
    {
        if (isServer)
        {
            CheckForPlayer();
        }

        if (isClient)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Rotation, 0.3f);
        }
    }

    [Server]
    private void CheckForPlayer()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 5f);
        if (colliders.Length > 0)
        {
            foreach (Collider item in colliders)
            {
                var PlayerController = item.gameObject.GetComponent<PlayerController>();
                if (PlayerController != null)
                {
                    transform.LookAt(new Vector3(PlayerController.transform.position.x, transform.position.y, PlayerController.transform.position.z));
                    Rotation = transform.rotation;
                    NetAnim.SetTrigger("Fire");
                }
            }
        }
    }
}
