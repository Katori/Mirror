using System.Linq;
using UnityEngine;

namespace Mirror.PongPlusPlus
{
    public class GameManagerComponent : NetworkBehaviour
    {
        internal static GameManagerComponent Instance { get; private set; }

        #region SyncVars

        [SyncVar(hook =nameof(Team1ScoreUpdated))]
        public int Team1Score;

        [SyncVar(hook = nameof(Team2ScoreUpdated))]
        public int Team2Score;

        #endregion

        #region Inspector Links

        [SerializeField]
        private Transform Team1Spawn = default;

        [SerializeField]
        private Transform Team2Spawn = default;

        #endregion

        #region SyncObjects

        public SyncDictGameObjectInt PlayersAndIndividualScores = new SyncDictGameObjectInt();

        public class SyncDictGameObjectInt : SyncDictionary<GameObject, Player> { }

        #endregion

        #region Private Variables

        private Rigidbody BallRb;

        private Vector3 Offset = new Vector3(0, 2.73f, -3.44f);

        private bool TrackPlayer;

        private Transform PlayerTransform;
        private bool GameStarted;

        private int LastTeamSpawned = 1;

        #endregion

        #region Mirror Callbacks

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetInstance();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SetInstance();
        }

        #endregion

        #region Private Methods

        private void SetInstance()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region SyncVar Hooks

        public void Team1ScoreUpdated(int NewScore)
        {
            UIControllerComponent.Instance.UpdateTeam1Score(NewScore);
        }

        public void Team2ScoreUpdated(int NewScore)
        {
            UIControllerComponent.Instance.UpdateTeam2Score(NewScore);
        }

        #endregion

        #region Server Methods

        [Server]
        internal void Score(GameObject player, int Team)
        {
            var ScoredPlayer = PlayersAndIndividualScores[player];
            ScoredPlayer.Score++;
            PlayersAndIndividualScores[player] = ScoredPlayer;
            var scoredPlayerComponent = player.GetComponent<PlayerComponent>();
            scoredPlayerComponent.Score++;
            scoredPlayerComponent.PlayScoreSound();
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
            int PlayerTeam;
            if (LastTeamSpawned == 1)
            {
                PlayerTeam = 0;
                LastTeamSpawned = 0;
            }
            else
            {
                PlayerTeam = 1;
                LastTeamSpawned = 1;
            }
            if (PlayerTeam == 0)
            {
                InstantiatePlayer(0, playerPrefab, conn);
            }
            else
            {
                InstantiatePlayer(1, playerPrefab, conn);
            }

            if (PlayersAndIndividualScores.Count > 1 && !GameStarted)
            {
                var RandomPlayerIndex = Random.Range(0, PlayersAndIndividualScores.Count);
                PlayersAndIndividualScores.ToList()[RandomPlayerIndex].Key.GetComponent<PlayerComponent>().CanServe = true;
                GameStarted = true;
            }
        }

        [Server]
        private void InstantiatePlayer(int Team, GameObject playerPrefab, NetworkConnection conn)
        {
            GameObject player;
            if (Team == 0)
            {
                player = Instantiate(playerPrefab, Team1Spawn.position, Team1Spawn.rotation);
            }
            else
            {
                player = Instantiate(playerPrefab, Team2Spawn.position, Team2Spawn.rotation);
            }
            player.GetComponent<PlayerComponent>().Team = Team;
            NetworkServer.AddPlayerForConnection(conn, player);
            PlayersAndIndividualScores.Add(player, new Player { Score = 0, Team = Team });
        }

        [Server]
        internal void BallOut()
        {
            GenerateServe(Random.Range(0, 2));
        }

        #endregion

        #region Structs

        [System.Serializable]
        public struct Player
        {
            public int Score;
            public int Team;
        }

        #endregion
    }
}
