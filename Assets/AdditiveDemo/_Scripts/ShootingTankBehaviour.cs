using UnityEngine;
using Mirror;

public class ShootingTankBehaviour : NetworkBehaviour
{
    [SyncVar(hook = nameof(RotationUpdated))]
    public Quaternion Rotation;

    private Animator Anim;

    private void Start()
    {
        if (isServer)
        {
            Anim = GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        CheckForPlayer();
    }

    public override void OnSetLocalVisibility(bool vis)
    {
        base.OnSetLocalVisibility(vis);
        if (vis)
        {
            CheckForPlayer();
        }
    }

    [Server]
    private void CheckForPlayer()
    {
        if (Physics.SphereCast(transform.position, 6, transform.forward, out RaycastHit hit))
        {
            var c = hit.collider.GetComponent<PlayerController>();
            if (c != null)
            {
                Rotation = transform.rotation;
                Anim.SetTrigger("Fire");
            }
        }
    }

    public void RotationUpdated(Quaternion NewRotation)
    {
        transform.rotation = NewRotation;
    }
}
