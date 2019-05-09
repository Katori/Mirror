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

        [SyncVar]
        public int Team;

        [SyncVar(hook =nameof(ScoreUpdated))]
        public int Score;

        public void ScoreUpdated(int newScore)
        {
            UIControllerComponent.Instance.PlayerScoreUpdated(newScore);
        }

        [SyncVar]
        public bool CanServe;
        private const float ServeSpeed = 3f;
        private const float Speed = 3f;

        public override void OnStartClient()
        {
            base.OnStartClient();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            MeshRenderer.material = LocalPlayerMaterial;
            GameManagerComponent.Instance.DisableSceneCamera();
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

                if (CanServe)
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
                FindObjectOfType<BallComponent>().Rb.AddForce(transform.forward * ServeSpeed);
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
