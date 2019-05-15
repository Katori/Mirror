using UnityEngine;

namespace Mirror.PongPlusPlus
{
    class PongPlusPlusNetworkManager : NetworkManager
    {
        [SerializeField]
        private GameObject SceneCamera = default;

        internal void DisableSceneCamera()
        {
            SceneCamera.SetActive(false);
        }

        internal void EnableSceneCamera()
        {
            SceneCamera.SetActive(true);
        }

        public override void OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage)
        {
            GameManagerComponent.Instance.AddPlayer(conn, playerPrefab);
        }

        public override void OnServerRemovePlayer(NetworkConnection conn, NetworkIdentity player)
        {
            base.OnServerRemovePlayer(conn, player);
            GameManagerComponent.Instance.RemovePlayer(conn, player);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            EnableSceneCamera();
            UIControllerComponent.Instance.DeactivateServePanel();
            UIControllerComponent.Instance.ShowConnectPanel();
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            EnableSceneCamera();
            UIControllerComponent.Instance.ShowConnectPanel();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            UIControllerComponent.Instance.ShowConnectPanel();
        }
    }
}
