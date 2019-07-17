using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RigidbodyCCPlayerBehaviour : NetworkRigidbody
{
    [SerializeField]
    private GameObject Camera;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Camera.SetActive(true);
    }
}
