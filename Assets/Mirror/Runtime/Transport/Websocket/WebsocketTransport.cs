// wraps Telepathy for use as HLAPI TransportLayer
using System;
using UnityEngine;

namespace Mirror
{
    public class WebsocketTransport : Transport
    {

        protected Client client = new Client();
        protected Server server = new Server();

        public int port;

        public bool Secure = false;

        public string CertificatePath;

        public string CertificatePassword;

        public WebsocketTransport()
        {
            // dispatch the events from the server
            server.Connected += (id) => OnServerConnected.Invoke(id);
            server.Disconnected +=(id)=>OnServerDisconnected.Invoke(id);
            server.ReceivedData += (id, data) => OnServerDataReceived.Invoke(id, data);
            server.ReceivedError += (id, exception) => OnServerError?.Invoke(id, exception);

            // dispatch events from the client
            client.Connected += () => OnClientConnected.Invoke();
            client.Disconnected += () => OnClientDisconnected.Invoke();
            client.ReceivedData += (data) => OnClientDataReceived.Invoke(data);
            client.ReceivedError += (exception) => OnClientError?.Invoke(exception);

            // HLAPI's local connection uses hard coded connectionId '0', so we
            // need to make sure that external connections always start at '1'
            // by simple eating the first one before the server starts
            Server.NextConnectionId();

            Debug.Log("Websocket transport initialized!");
        }

        public override bool Available()
        {
            // WebSockets should be available on all platforms, including WebGL (automatically) using our included JSLIB code
            return true;
        }

        // client
        public override bool ClientConnected() { return client.IsConnected; }
        public override void ClientConnect(string host)
        {
            if (Secure)
            {
                client.Connect(new Uri($"wss://{host}:{port}"));
            }
            else
            {
                client.Connect(new Uri($"ws://{host}:{port}"));
            }
        }
        public override bool ClientSend(int channelId, byte[] data) { client.Send(data); return true; }
        public override void ClientDisconnect() { client.Disconnect(); }

        // server
        public override bool ServerActive() { return server.Active; }
        public override void ServerStart()
        {
            
            if (Secure)
            {
                server._secure = Secure;
                server._sslConfig = new Server.SslConfiguration
                {
                    Certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(Application.dataPath + CertificatePath, CertificatePassword),
                    ClientCertificateRequired = false,
                    CheckCertificateRevocation = false,
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Default
                };
            }
            server.Listen(port);
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data) { server.Send(connectionId, data); return true;  }

        public override bool ServerDisconnect(int connectionId)
        {
            return server.Disconnect(connectionId);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            return server.GetClientAddress(connectionId);
        }
        public override void ServerStop() { server.Stop(); }

        // common
        public override void Shutdown()
        {
            client.Disconnect();
            server.Stop();
        }

        public override int GetMaxPacketSize(int channelId)
        {
            // Telepathy's limit is Array.Length, which is int
            return int.MaxValue;
        }

        public override string ToString()
        {
            if (client.Connecting || client.IsConnected)
            {
                return client.ToString();
            }
            if (server.Active)
            {
                return server.ToString();
            }
            return "";
        }
    }
}