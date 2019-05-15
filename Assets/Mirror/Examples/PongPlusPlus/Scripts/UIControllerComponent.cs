using UnityEngine;
using UnityEngine.UI;
using Mirror.Websocket;

namespace Mirror.PongPlusPlus
{
    public class UIControllerComponent : MonoBehaviour
    {
        internal static UIControllerComponent Instance { get; private set; }

        [SerializeField]
        private Text ScoreText = default;

        [SerializeField]
        private GameObject ServePanel = default;

        [SerializeField]
        private GameObject ConnectPanel = default;

        [SerializeField]
        private InputField AddressInput = default;

        [SerializeField]
        private InputField PortInput = default;

        [SerializeField]
        private GameObject SecureTogglePanel = default;

        [SerializeField]
        private Toggle SecureToggle = default;

        [SerializeField]
        private bool ShowSecureCheckbox = false;

        private int Team1Score;
        private int Team2Score;
        private int PlayerScore;

        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
            if (ShowSecureCheckbox)
            {
                SecureTogglePanel.SetActive(true);
            }
        }

        internal void ShowConnectPanel()
        {
            ConnectPanel.SetActive(true);
        }

        internal void ActivateServePanel()
        {
            ServePanel.SetActive(true);
        }

        internal void DeactivateServePanel()
        {
            ServePanel.SetActive(false);
        }

        internal void PlayerScoreUpdated(int newScore)
        {
            PlayerScore = newScore;
            ScoreText.text = "Team 1: " + Team1Score + "\nTeam 2: " + Team2Score + "\nPersonal: " + PlayerScore;
        }

        internal void UpdateTeam1Score(int newScore)
        {
            Team1Score = newScore;
            ScoreText.text = "Team 1: " + Team1Score + "\nTeam 2: " + Team2Score + "\nPersonal: " + PlayerScore;
        }

        internal void UpdateTeam2Score(int newScore)
        {
            Team2Score = newScore;
            ScoreText.text = "Team 1: " + Team1Score + "\nTeam 2: " + Team2Score + "\nPersonal: " + PlayerScore;
        }

        public void ConnectButtonPressed()
        {
            ConnectPanel.SetActive(false);
            var port = int.Parse(PortInput.text);
            var address = AddressInput.text;
            if(NetworkManager.singleton.GetComponent<TelepathyTransport>() != null)
            {
                var telepathyTransport = NetworkManager.singleton.GetComponent<TelepathyTransport>();
                telepathyTransport.port = (ushort)port;
            }
            if (NetworkManager.singleton.GetComponent<WebsocketTransport>() != null)
            {
                var webSocketTransport = NetworkManager.singleton.GetComponent<WebsocketTransport>();
                webSocketTransport.port = port;
                if (SecureToggle.isOn)
                {
                    webSocketTransport.Secure = true;
                }
            }
            NetworkManager.singleton.networkAddress = address;
            NetworkManager.singleton.StartClient();
        }

        public void StartServerButtonPressed()
        {
            ConnectPanel.SetActive(false);
            var port = int.Parse(PortInput.text);
            if (NetworkManager.singleton.GetComponent<TelepathyTransport>() != null)
            {
                var telepathyTransport = NetworkManager.singleton.GetComponent<TelepathyTransport>();
                telepathyTransport.port = (ushort)port;
            }
            if (NetworkManager.singleton.GetComponent<WebsocketTransport>() != null)
            {
                var webSocketTransport = NetworkManager.singleton.GetComponent<WebsocketTransport>();
                webSocketTransport.port = port;
                if (SecureToggle.isOn)
                {
                    webSocketTransport.Secure = true;
                }
            }
            NetworkManager.singleton.StartServer();
        }

        public void StartHostButtonPressed()
        {
            ConnectPanel.SetActive(false);
            var port = int.Parse(PortInput.text);
            if (NetworkManager.singleton.GetComponent<TelepathyTransport>() != null)
            {
                var telepathyTransport = NetworkManager.singleton.GetComponent<TelepathyTransport>();
                telepathyTransport.port = (ushort)port;
            }
            if (NetworkManager.singleton.GetComponent<WebsocketTransport>() != null)
            {
                var webSocketTransport = NetworkManager.singleton.GetComponent<WebsocketTransport>();
                webSocketTransport.port = port;
                if (SecureToggle.isOn)
                {
                    webSocketTransport.Secure = true;
                }
            }
            NetworkManager.singleton.StartHost();
        }
    }
}
