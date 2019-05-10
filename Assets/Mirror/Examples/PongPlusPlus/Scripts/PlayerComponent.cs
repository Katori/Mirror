using System;
using UnityEngine;

namespace Mirror.PongPlusPlus
{
    public class PlayerComponent : NetworkBehaviour
    {
        [SerializeField]
        private Renderer MeshRenderer;

        [SerializeField]
        private Material LocalPlayerMaterial;

        [SerializeField]
        private Rigidbody Rb;

        [SerializeField]
        private GameObject Camera;

        [SerializeField]
        private GameObject BallPrefab;

        [SyncVar]
        public int Team;

        [SyncVar(hook =nameof(ScoreUpdated))]
        public int Score;

        public void ScoreUpdated(int newScore)
        {
            UIControllerComponent.Instance.PlayerScoreUpdated(newScore);
        }

        [SyncVar(hook =nameof(ServeEnableChanged))]
        public bool CanServe;

        private void ServeEnableChanged(bool NewServe)
        {
            if (NewServe)
            {
                UIControllerComponent.Instance.ActivateServePanel();
            }
            else
            {
                UIControllerComponent.Instance.DeactivateServePanel();
            }
        }

        private const float ServeSpeed = 250f;
        private const float Speed = 3f;

        public override void OnStartClient()
        {
            base.OnStartClient();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            MeshRenderer.material = LocalPlayerMaterial;
            FindObjectOfType<PongPlusPlusNetworkManager>().DisableSceneCamera();
            Camera.SetActive(true);
            if (CanServe)
            {
                UIControllerComponent.Instance.ActivateServePanel();
            }
        }

        private void Update()
        {
            if (isLocalPlayer)
            {
                if (Input.GetKey(KeyCode.A))
                {
                    CmdMove(-transform.up);
                }
                if (Input.GetKey(KeyCode.D))
                {
                    CmdMove(transform.up);
                }

                if (CanServe && Input.GetKey(KeyCode.Space))
                {
                    CmdServeBall();
                }
            }
        }

        [Command]
        public void CmdServeBall()
        {
            if (CanServe)
            {
                var ball = Instantiate(BallPrefab, transform.position + transform.forward, Quaternion.identity);
                NetworkServer.Spawn(ball);
                var ballComponent = ball.GetComponent<BallComponent>();
                ballComponent.playerKicked = gameObject;
                ballComponent.Rb.AddForce(transform.forward * ServeSpeed);
                CanServe = false;
            }
        }

        [Command]
        public void CmdMove(Vector3 vector3)
        {
            Rb.MovePosition(Rb.position + vector3*Time.deltaTime*Speed);
        }
    }
}
