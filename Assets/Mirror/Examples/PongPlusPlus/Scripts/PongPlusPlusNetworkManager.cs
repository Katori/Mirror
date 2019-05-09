using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror.PongPlusPlus
{
    class PongPlusPlusNetworkManager : NetworkManager
    {
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
            GameManagerComponent.Instance.EnableSceneCamera();
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            GameManagerComponent.Instance.EnableSceneCamera();
        }
    }
}
