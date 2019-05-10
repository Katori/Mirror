using System.Linq;
using UnityEngine;

namespace Mirror.PongPlusPlus
{
    public class GameManagerComponent : NetworkBehaviour
    {
        internal static GameManagerComponent Instance { get; private set; }

        [SyncVar(hook =nameof(Team1ScoreUpdated))]
        public int Team1Score;

        public void Team1ScoreUpdated(int NewScore)
        {
            UIControllerComponent.Instance.UpdateTeam1Score(NewScore);
        }

        public void Team2ScoreUpdated(int NewScore)
        {
            UIControllerComponent.Instance.UpdateTeam2Score(NewScore);
        }

        [SyncVar(hook = nameof(Team2ScoreUpdated))]
        public int Team2Score;

        [SerializeField]
        private Transform Team1Spawn;

        [SerializeField]
        private Transform Team2Spawn;

        [SerializeField]
        private GameObject Ball;

        public SyncDictGameObjectInt PlayersAndIndividualScores = new SyncDictGameObjectInt();

        public class SyncDictGameObjectInt : SyncDictionary<GameObject, int> { }

        private Rigidbody BallRb;

        private Vector3 Offset = new Vector3(0, 2.73f, -3.44f);

        private bool TrackPlayer;

        private Transform PlayerTransform;
        private bool GameStarted;

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetInstance();
        }

        private void SetInstance()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SetInstance();
        }

        [Server]
        internal void Score(GameObject player, int Team)
        {
            PlayersAndIndividualScores[player] ++;
            player.GetComponent<PlayerComponent>().Score++;
            if (Team == 0)
            {
                Team1Score++;
            }
            else
            {
                Team2Score++;
            }
        }

        [Server]
        internal void RemovePlayer(NetworkConnection conn, NetworkIdentity player)
        {
            PlayersAndIndividualScores.Remove(player.gameObject);
            if (PlayersAndIndividualScores.Count == 0)
            {
                GameStarted = false;
            }
        }

        [Server]
        internal void AddPlayer(NetworkConnection conn, GameObject playerPrefab)
        {
            var PlayerTeam = Random.Range(0, 2);
            if (PlayerTeam == 0)
            {
                GameObject player = Instantiate(playerPrefab, Team1Spawn.position, Team1Spawn.rotation);
                player.GetComponent<PlayerComponent>().Team = 0;
                NetworkServer.AddPlayerForConnection(conn, player);
                PlayersAndIndividualScores.Add(player, 0);
            }
            else
            {
                GameObject player = Instantiate(playerPrefab, Team2Spawn.position, Team2Spawn.rotation);
                player.GetComponent<PlayerComponent>().Team = 0;
                NetworkServer.AddPlayerForConnection(conn, player);
                PlayersAndIndividualScores.Add(player, 0);
            }

            if (PlayersAndIndividualScores.Count > 2 && !GameStarted)
            {
                var RandomPlayerIndex = Random.Range(0, PlayersAndIndividualScores.Count);
                PlayersAndIndividualScores.ToList()[RandomPlayerIndex].Key.GetComponent<PlayerComponent>().CanServe = true;
                GameStarted = true;
            }
        }
    }
}
