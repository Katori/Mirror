using UnityEngine;
using Mirror;

namespace Mirror.Examples.CharacterController
{
    public class NetworkCharacterControllerExt : NetworkCharacterController
    {
        [Header("Custom Settings")]
        public Camera playerCamera;

        [SyncVar(hook = nameof(SetColor))]
        public Color playerColor = Color.black;

        void SetColor(Color color)
        {
            GetComponent<Renderer>().material.color = color;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Set the player's color
            playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Call the SyncVar hook because it won't fire on join automatically
            SetColor(playerColor);
        }

        public override void OnStartLocalPlayer()
        {
            Debug.Log("OnStartLocalPlayer");
            playerCamera.gameObject.SetActive(true);
            base.OnStartLocalPlayer();
        }
    }
}
