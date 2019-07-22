using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RigidbodyCCNetworkManager : NetworkManager
{
    [SerializeField]
    private GameObject Camera;

    public override void OnStartClient()
    {
        base.OnStartClient();
        Camera.SetActive(false);
    }
}
