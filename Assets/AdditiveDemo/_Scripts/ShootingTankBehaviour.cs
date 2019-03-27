using UnityEngine;
using Mirror;
using System;

public class ShootingTankBehaviour : NetworkBehaviour
{
    [SyncVar]
    public Quaternion Rotation;

    private Animator Anim;

    [ServerCallback]
    private void Start()
    {
        Anim = GetComponent<Animator>();
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
        Collider[] colliders = Physics.OverlapSphere(transform.position, 4f);
        if (colliders.Length > 0)
        {
            foreach (Collider item in colliders)
            {
                var c = item.gameObject.GetComponent<PlayerController>();
                if (c != null)
                {
                    transform.LookAt(new Vector3(c.transform.position.x, transform.position.y, c.transform.position.z));
                    Rotation = transform.rotation;
                    Anim.SetTrigger("Fire");
                }
            }
        }
    }
}
