using UnityEngine;
using Mirror;

namespace Mirror.Examples.CharacterController
{
    public class NetworkManagerExt : NetworkManager
    {
        GameObject mainCamera;

        void Start()
        {
            Application.targetFrameRate = 60;
            mainCamera = Camera.main.gameObject;
        }

        public override void OnClientConnect(NetworkConnection conn)
        {
            base.OnClientConnect(conn);
        }

        public override void OnClientDisconnect(NetworkConnection conn)
        {
            mainCamera.SetActive(true);
            base.OnClientDisconnect(conn);
        }
    }
}
