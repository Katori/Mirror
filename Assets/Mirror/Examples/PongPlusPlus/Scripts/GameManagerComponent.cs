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

        public SyncDictGameObjectInt PlayersAndIndividualScores = new SyncDictGameObjectInt();

        public class SyncDictGameObjectInt : SyncDictionary<GameObject, Player> { }

        [System.Serializable]
        public struct Player
        {
            public int Score;
            public int Team;
        }

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
            else if(Instance!=this)
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
            var c = PlayersAndIndividualScores[player];
            c.Score++;
            PlayersAndIndividualScores[player] = c;
            player.GetComponent<PlayerComponent>().Score++;
            if (Team == 0)
            {
                Team1Score++;
                GenerateServe(0);
            }
            else
            {
                Team2Score++;
                GenerateServe(1);
            }
        }

        [Server]
        private void GenerateServe(int v)
        {
            var c = PlayersAndIndividualScores.Where(x => x.Value.Team == v).ToList();
            if (c.Count > 0)
            {
                var p = Random.Range(0, c.Count);
                var newServerPlayer = c[p];
                newServerPlayer.Key.GetComponent<PlayerComponent>().CanServe = true;
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
                PlayersAndIndividualScores.Add(player, new Player { Score = 0, Team = 0});
            }
            else
            {
                GameObject player = Instantiate(playerPrefab, Team2Spawn.position, Team2Spawn.rotation);
                player.GetComponent<PlayerComponent>().Team = 0;
                NetworkServer.AddPlayerForConnection(conn, player);
                PlayersAndIndividualScores.Add(player, new Player {Score = 0, Team = 1 });
            }

            if (PlayersAndIndividualScores.Count > 1 && !GameStarted)
            {
                Debug.LogWarning("Should start game");
                var RandomPlayerIndex = Random.Range(0, PlayersAndIndividualScores.Count);
                PlayersAndIndividualScores.ToList()[RandomPlayerIndex].Key.GetComponent<PlayerComponent>().CanServe = true;
                GameStarted = true;
            }
            else
            {
                Debug.LogWarning("shouldn't start game" + GameStarted+" "+PlayersAndIndividualScores.Count);
            }
        }
    }
}
