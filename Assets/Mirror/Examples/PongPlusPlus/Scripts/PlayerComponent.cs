using System;
using UnityEngine;

namespace Mirror.PongPlusPlus
{
    public class PlayerComponent : NetworkBehaviour
    {
        #region Inspector Links

        [SerializeField]
        private Renderer MeshRenderer = default;

        [SerializeField]
        private Material LocalPlayerMaterial = default;

        [SerializeField]
        private Rigidbody Rb = default;

        [SerializeField]
        private GameObject Camera = default;

        [SerializeField]
        private GameObject BallPrefab = default;

        #endregion

        #region SyncVars

        [SyncVar]
        public int Team;

        [SyncVar(hook =nameof(ScoreUpdated))]
        public int Score;

        [SyncVar(hook = nameof(ServeEnableChanged))]
        public bool CanServe;

        #endregion

        #region Constants

        private const float ServeSpeed = 250f;
        private const float Speed = 3f;

        #endregion

        #region SyncVar Hooks

        public void ScoreUpdated(int newScore)
        {
            if (isLocalPlayer)
            {
                UIControllerComponent.Instance.PlayerScoreUpdated(newScore);
            }
        }

        public void ServeEnableChanged(bool NewServe)
        {
            if (isLocalPlayer)
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
        }

        #endregion

        #region Mirror Callbacks

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

        #endregion

        #region Unity Callbacks

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

        #endregion

        #region Commands

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

        #endregion
    }
}
