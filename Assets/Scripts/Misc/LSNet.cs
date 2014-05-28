// The Less Shitty Networking system.
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using ProtoBuf;

[ProtoContract]
public class NetworkMessage {
        public enum Type {
                // Server->client, sends player ID.
                Hello = 1,
                // Server->client, sends player ID.
                PlayerJoin = 2,
                // Server->client, sends player ID.
                PlayerLeave = 3,
                // Bidirectional, sends player ID, name and teamID.
                PlayerUpdate = 4,
                // Bidirectional, sends levelName.
                StartGame = 5,
                // Command.
                SpawnEntity = 6,
                // Command.
                Move = 7,
                // Command.
                Attack = 8,
                // Command.
                UIAction = 9,
                // Client->server.
                Ready = 10,
                // Server->client.
                NextTurn = 11,
        }

        public NetworkMessage(Type type) {
                this.type = type;
        }

        public byte[] Serialize() {
                var stream = new MemoryStream();
                stream.WriteByte(0);
                stream.WriteByte(0);
                Serializer.Serialize(stream, this);
                var buffer = stream.ToArray();
                buffer[0] = (byte)(buffer.Length & 0xFF);
                buffer[1] = (byte)(buffer.Length >> 8);
                return stream.ToArray();
        }

        static public NetworkMessage Deserialize(byte[] buffer) {
                var len = (int)buffer[0] | ((int)buffer[1] << 8);
                var stream = new MemoryStream(buffer, 2, len);
                return Serializer.Deserialize<NetworkMessage>(stream);
        }

        [ProtoMember(1)]
        public Type type;

        [ProtoMember(2)]
        public int playerID = -1;

        [ProtoMember(3)]
        public string playerName = "";

        [ProtoMember(4)]
        public int teamID = -1;

        // Level to load.
        [ProtoMember(5)]
        public string levelName;

        // Turn this command must be executed on.
        [ProtoMember(6)]
        public int turnID;

        // ID of this command, increases by 1 with each command sent.
        [ProtoMember(7)]
        public int commandID;

        // Name of the entity to spawn. Must be a Unity Resource.
        [ProtoMember(8)]
        public string entityName;

        // ID of the entity being ordered.
        [ProtoMember(9)]
        public int entityID;

        [ProtoMember(10)]
        public DVector2 position;

        [ProtoMember(11)]
        public DReal rotation;

        // ID of the entity being targetted.
        [ProtoMember(12)]
        public int targetID;

        [ProtoMember(13)]
        public int UIwhat;
}

public class NetworkClient {}

public interface IServer {
        void OnServerActive(LSNet net);
        void OnClientConnect(NetworkClient client);
        void OnClientDisconnect(NetworkClient client);
        void OnClientMessage(NetworkClient client, NetworkMessage message);
}

public interface IClient {
        void OnConnected(LSNet net);
        void OnDisconnected();
        void OnFailedToConnect();
        void OnServerMessage(NetworkMessage message);
}

class ClientData {
        public ClientData(NetworkClient client, Socket socket) {
                this.client = client;
                this.socket = socket;
                this.buffer = new byte[0x10000 + 2]; // max message size plus length
                this.connected = true;
        }

        public NetworkClient client;
        public Socket socket;
        public byte[] buffer;
        public int messageLength;
        public bool connected;
        public IAsyncResult activeReceive;
}

// Beware! Async callbacks may be invoked in seperate threads, so can't directly call back into Unity.
public class LSNet : UnityEngine.MonoBehaviour {
        public bool isServer {
                get {
                        return listenSocket != null;
                }
        }

        public bool isClient {
                get {
                        return localClient != null;
                }
        }

        private Socket listenSocket;
        private IServer server;
        private NetworkClient serverClient;
        private Dictionary<NetworkClient, ClientData> clientSockets;

        private IClient localClient;

        private List<Action> pendingUnityActions = new List<Action>();

        void OnDestroy() {
                Disconnect();
        }

        // Call Unity-side to push events through properly.
        void Update() {
                List<Action> actions;
                lock(pendingUnityActions) {
                        actions = new List<Action>(pendingUnityActions);
                        pendingUnityActions.Clear();
                }
                foreach(var act in actions) {
                        try {
                                act();
                        } catch(Exception e) {
                                UnityEngine.Debug.LogException(e, null);
                        }
                }
        }

        // If server, disconnect all clients and close the listening socket.
        // If client, disconnect from the server.
        public void Disconnect() {
                /*
                if(server != null) {
                        foreach(var data in clientSockets.Values) {
                                if(data.disconnected) continue;
                                data.disconnected = true;

                        }
                } else if(localClient) {
                }
                */
        }

        public void InitializeServer(int port, IServer server, IClient localClient) {
                if(isServer || isClient) {
                        throw new Exception("Networking already initialized.");
                }

                var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                var localEP = new IPEndPoint(ipHostInfo.AddressList[0], port);
                listenSocket = new Socket(localEP.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                try {
                        listenSocket.Bind(localEP);
                        listenSocket.Listen(10);
                        listenSocket.BeginAccept(new AsyncCallback(OnClientConnect), listenSocket);
                } catch(Exception e) {
                        UnityEngine.Debug.LogError("Failed to start server on port " + port + ": " + e);
                        listenSocket = null;
                        throw;
                }

                this.server = server;
                clientSockets = new Dictionary<NetworkClient, ClientData>();
                AddUnityAction(() => server.OnServerActive(this));
                if(localClient != null) {
                        serverClient = new NetworkClient();
                        this.localClient = localClient;
                        AddUnityAction(() => { localClient.OnConnected(this); });
                        AddUnityAction(() => { server.OnClientConnect(serverClient); });
                }
        }

        public void Connect(string host, int port, IClient client) {
        //        if(server != null) {
        //                throw new Exception("Already hosting a server. If you want to connect to the local server, use the localClient parameter to InitializeServer.");
        //        }
        //        ???;
        }

        public void CloseConnection(NetworkClient client) {

        }

        private void SendMessageToClient(ClientData data, byte[] buffer) {
                data.socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, OnSendClientComplete, data);
        }

        // Send a message from the server to a specific client.
        public void SendMessageToClient(NetworkClient target, NetworkMessage message) {
                if(target == serverClient) {
                        AddUnityAction(() => { localClient.OnServerMessage(message); });
                        return;
                }

                var buffer = message.Serialize();

                SendMessageToClient(clientSockets[target], buffer);
        }

        // Send a message from the server to all clients (including the local client)
        public void SendMessageToAll(NetworkMessage message) {
                if(serverClient != null) {
                        AddUnityAction(() => { localClient.OnServerMessage(message); });
                }

                var buffer = message.Serialize();

                foreach(var data in clientSockets.Values) {
                        SendMessageToClient(data, buffer);
                }
        }

        // Send a message from the client to the server.
        public void SendMessageToServer(NetworkMessage message) {
                if(server != null) {
                        if(serverClient == null) {
                                throw new Exception("Hosting server, but not connected. Use the localClient parameter to InitializeServer.");
                        }
                        AddUnityAction(() => { server.OnClientMessage(serverClient, message); });
                        return;
                }
        }

        private void AddUnityAction(Action action) {
                lock(pendingUnityActions) {
                        pendingUnityActions.Add(action);
                }
        }

        // Server async callbacks.
        private void OnSendClientComplete(IAsyncResult info) {
                var data = info.AsyncState as ClientData;
                data.socket.EndSend(info);
        }

        private void OnReceiveClientHeader(IAsyncResult info) {
                var client = info.AsyncState as NetworkClient;
                var data = clientSockets[client];
                data.messageLength = (int)data.buffer[0] | ((int)data.buffer[1] << 8);

                data.activeReceive = data.socket.BeginReceive(data.buffer, 2, data.messageLength, SocketFlags.None, OnReceiveClientMessage, client);
        }

        private void OnReceiveClientMessage(IAsyncResult info) {
                var client = info.AsyncState as NetworkClient;
                var data = clientSockets[client];

                var message = NetworkMessage.Deserialize(data.buffer);
                AddUnityAction(() => { server.OnClientMessage(client, message); });

                data.activeReceive = data.socket.BeginReceive(data.buffer, 0, 2, SocketFlags.None, OnReceiveClientHeader, client);
        }

        private void OnClientConnect(IAsyncResult result) {
                try {
                        var socket = listenSocket.EndAccept(result);
                        AddUnityAction(() => {
                                        var client = new NetworkClient();
                                        var data = new ClientData(client, socket);
                                        clientSockets[client] = data;
                                        server.OnClientConnect(client);
                                        data.activeReceive = socket.BeginReceive(data.buffer, 0, 2, SocketFlags.None, OnReceiveClientHeader, client);
                                });
                } catch(Exception e) {
                        AddUnityAction(() => { UnityEngine.Debug.LogError("Failed to accept incoming connection: " + e); });
                }

                try {
                        listenSocket.BeginAccept(new AsyncCallback(OnClientConnect), listenSocket);
                } catch(Exception e) {
                        AddUnityAction(() => { UnityEngine.Debug.LogError("Failed when starting new accept process: " + e); });
                }
        }

        // Client async callbacks.
}
