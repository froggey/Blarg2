// The Less Shitty Networking system.
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;

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
                // Client->server, sends player ID.
                KickPlayer = 5,
                // Bidirectional, sends levelName.
                StartGame = 6,
                // Command.
                SpawnEntity = 7,
                // Command.
                Move = 8,
                // Command.
                Attack = 9,
                // Command.
                UIAction = 10,
                // Client->server. Optionally sends gameState for sync checking.
                Ready = 11,
                // Server->client.
                NextTurn = 12,
                // Command.
                SetPowerState = 13,
        }

        public NetworkMessage() {}

        public NetworkMessage(Type type) {
                this.type = type;
        }

        public byte[] Serialize() {
                var stream = new MemoryStream();
                stream.WriteByte(0);
                stream.WriteByte(0);
                Serializer.Serialize(stream, this);
                var buffer = stream.ToArray();
                var len = buffer.Length - 2;
                if(len >= 0x10000) {
                        throw new Exception("Message exceeds maximum packet length!");
                }
                buffer[0] = (byte)(len & 0xFF);
                buffer[1] = (byte)(len >> 8);
                return buffer;
        }

        static public NetworkMessage Deserialize(byte[] buffer) {
                var len = (int)buffer[0] | ((int)buffer[1] << 8);
                var stream = new MemoryStream(buffer, 2, len);
                return Serializer.Deserialize<NetworkMessage>(stream);
        }

        [ProtoMember(1)]
        public Type type;

        [ProtoMember(2)]
        public int playerID;

        [ProtoMember(3)]
        public string playerName;

        [ProtoMember(4)]
        public int teamID;

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

        // Serialized game state.
        [ProtoMember(14)]
        public string gameState;

        [ProtoMember(15)]
        public bool powerState;
}

public class NetworkClient {}

public interface IServer {
        // Called when the server starts accepting connections.
        void OnServerActive(LSNet net);
        // Called when a client connects.
        void OnClientConnect(NetworkClient client);
        // Called when a client is disconnected.
        void OnClientDisconnect(NetworkClient client);
        // Called when a message is received from the client.
        void OnClientMessage(NetworkClient client, NetworkMessage message);
}

public interface IClient {
        // Called when connected to the server.
        void OnConnected(LSNet net);
        // Called when disconnected from the server.
        void OnDisconnected();
        // Called when unable to connect to the server.
        void OnFailedToConnect();
        // Called when a message is received from the server.
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
        public int bytesRead;
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
        private Socket clientSocket;
        private int clientMessageLength;
        private int clientBytesRead;
        private byte[] clientBuffer = new byte[0x10000 + 2];

        private List<Action> pendingUnityActions = new List<Action>();

        void OnDestroy() {
                Disconnect();
        }

        void OnApplicationQuit() {
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
                if(server != null) {
                        // Hosting a server.
                        foreach(var data in clientSockets.Values) {
                                //if(data.disconnected) continue;
                                //data.disconnected = true;
                                data.socket.Shutdown(SocketShutdown.Both);
                                data.socket.Close();
                        }
                        listenSocket.Close();

                        listenSocket = null;
                        server = null;
                        serverClient = null;
                        clientSockets.Clear();
                        localClient = null;
                } else if(localClient != null) {
                        // Connected somewhere.
                        clientSocket.Shutdown(SocketShutdown.Both);
                        clientSocket.Close();
                }
        }

        public void InitializeServer(int port, IServer server, IClient localClient) {
                if(isServer || isClient) {
                        throw new Exception("Networking already initialized.");
                }

                var localEP = new IPEndPoint(IPAddress.Any, port);
                listenSocket = new Socket(localEP.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                UnityEngine.Debug.Log("Listening on " + localEP);

                try {
                        listenSocket.Bind(localEP);
                        listenSocket.Listen(10);
                        listenSocket.BeginAccept(new AsyncCallback(OnClientConnect), listenSocket);
                } catch(Exception e) {
                        UnityEngine.Debug.LogError("Failed to start server on " + localEP);
                        UnityEngine.Debug.LogException(e, this);
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
                if(isServer || isClient) {
                        throw new Exception("Networking already initialized.");
                }

                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                localClient = client;
                try {
                        clientSocket.BeginConnect(host, port, OnConnect, clientSocket);
                } catch(Exception) {
                        localClient = null;
                        throw;
                }
        }

        public void CloseConnection(NetworkClient client) {
                clientSockets[client].socket.Close();
                clientSockets.Remove(client);
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

                var buffer = message.Serialize();

                clientSocket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, OnSendServerComplete, clientSocket);
        }

        private void AddUnityAction(Action action) {
                lock(pendingUnityActions) {
                        pendingUnityActions.Add(action);
                }
        }

        // Called Unity-side when the client stops receiving stuff.
        private void ClientDisconnected(NetworkClient client) {
                clientSockets[client].socket.Close();
                clientSockets.Remove(client);
                server.OnClientDisconnect(client);
        }

        // Called Unity-side when the server stops receiving stuff.
        private void ServerDisconnected() {
                try {
                        clientSocket.Close();
                } catch(Exception e) {
                        UnityEngine.Debug.LogException(e, this);
                }
                var c = localClient;
                localClient = null;
                clientSocket = null;
                c.OnDisconnected();
        }

        // Server async callbacks.
        private void OnSendClientComplete(IAsyncResult info) {
                try {
                        var data = info.AsyncState as ClientData;
                        data.socket.EndSend(info);
                } catch(Exception e) {
                        AddUnityAction(() => { UnityEngine.Debug.LogException(e, this); });
                }
        }

        private void OnReceiveClientHeader(IAsyncResult info) {
                var client = info.AsyncState as NetworkClient;
                var data = clientSockets[client];

                try {
                        var bytesRead = data.socket.EndReceive(info);
                        if(bytesRead == 0) {
                                // Connection closed.
                                AddUnityAction(() => { ClientDisconnected(client); });
                                return;
                        }
                        data.bytesRead += bytesRead;
                        if(data.bytesRead == 2) {
                                data.messageLength = (int)data.buffer[0] | ((int)data.buffer[1] << 8);
                                data.bytesRead = 0;
                                data.activeReceive = data.socket.BeginReceive(data.buffer, 2, data.messageLength, SocketFlags.None, OnReceiveClientMessage, client);
                        } else {
                                // Short read. restart.
                                data.activeReceive = data.socket.BeginReceive(data.buffer, data.bytesRead, 2 - data.bytesRead,
                                                                              SocketFlags.None, OnReceiveClientHeader, client);
                        }
                } catch(Exception e) {
                        AddUnityAction(() => {
                                        UnityEngine.Debug.LogException(e, this);
                                        ClientDisconnected(client);
                                });
                }
        }

        private void OnReceiveClientMessage(IAsyncResult info) {
                var client = info.AsyncState as NetworkClient;
                var data = clientSockets[client];

                try {
                        var bytesRead = data.socket.EndReceive(info);
                        if(bytesRead == 0) {
                                // Connection closed.
                                AddUnityAction(() => { ClientDisconnected(client); });
                                return;
                        }
                        data.bytesRead += bytesRead;
                        if(data.bytesRead == data.messageLength) {
                                var message = NetworkMessage.Deserialize(data.buffer);
                                AddUnityAction(() => { server.OnClientMessage(client, message); });
                                data.bytesRead = 0;
                                data.activeReceive = data.socket.BeginReceive(data.buffer, 0, 2, SocketFlags.None, OnReceiveClientHeader, client);
                        } else {
                                // Short read. restart.
                                data.activeReceive = data.socket.BeginReceive(data.buffer, 2 + data.bytesRead, data.messageLength - data.bytesRead,
                                                                              SocketFlags.None, OnReceiveClientMessage, client);
                        }
                } catch(Exception e) {
                        AddUnityAction(() => {
                                        UnityEngine.Debug.LogException(e, this);
                                        ClientDisconnected(client);
                                });
                }
        }

        private void OnClientConnect(IAsyncResult result) {
                // listener may have been closed and listenSocket nulled out.
                var serverSocket = result.AsyncState as Socket;
                try {
                        var socket = serverSocket.EndAccept(result);
                        AddUnityAction(() => {
                                        var client = new NetworkClient();
                                        var data = new ClientData(client, socket);
                                        clientSockets[client] = data;
                                        server.OnClientConnect(client);
                                        data.bytesRead = 0;
                                        data.activeReceive = socket.BeginReceive(data.buffer, 0, 2, SocketFlags.None, OnReceiveClientHeader, client);
                                });
                } catch(ObjectDisposedException) {
                        // Connection closed, don't care no more.
                        return;
                } catch(Exception e) {
                        AddUnityAction(() => { UnityEngine.Debug.LogError("Failed to accept incoming connection: " + e); });
                }

                try {
                        serverSocket.BeginAccept(new AsyncCallback(OnClientConnect), serverSocket);
                } catch(Exception e) {
                        AddUnityAction(() => { UnityEngine.Debug.LogError("Failed when starting new accept process: " + e); });
                }
        }

        // Client async callbacks.
        private void OnSendServerComplete(IAsyncResult info) {
                try {
                        clientSocket.EndSend(info);
                } catch(Exception e) {
                        AddUnityAction(() => {
                                        UnityEngine.Debug.LogException(e, this);
                                        ServerDisconnected();
                                });
                }
        }

        private void OnReceiveServerHeader(IAsyncResult info) {
                try {
                        var bytesRead = clientSocket.EndReceive(info);
                        if(bytesRead == 0) {
                                AddUnityAction(() => ServerDisconnected());
                                return;
                        }
                        clientBytesRead += bytesRead;
                        if(clientBytesRead == 2) {
                                clientMessageLength = (int)clientBuffer[0] | ((int)clientBuffer[1] << 8);
                                clientBytesRead = 0;
                                clientSocket.BeginReceive(clientBuffer, 2, clientMessageLength, SocketFlags.None, OnReceiveServerMessage, clientSocket);
                        } else {
                                clientSocket.BeginReceive(clientBuffer, clientBytesRead, 2 - clientBytesRead,
                                                          SocketFlags.None, OnReceiveServerHeader, clientSocket);
                        }
                } catch(Exception e) {
                        AddUnityAction(() => {
                                        UnityEngine.Debug.LogException(e, this);
                                        ServerDisconnected();
                                });
                }
        }

        private void OnReceiveServerMessage(IAsyncResult info) {
                try {
                        var bytesRead = clientSocket.EndReceive(info);
                        if(bytesRead == 0) {
                                AddUnityAction(() => { ServerDisconnected(); });
                                return;
                        }

                        clientBytesRead += bytesRead;
                        if(clientBytesRead == clientMessageLength) {
                                var message = NetworkMessage.Deserialize(clientBuffer);
                                AddUnityAction(() => { localClient.OnServerMessage(message); });
                                clientBytesRead = 0;
                                clientSocket.BeginReceive(clientBuffer, 0, 2, SocketFlags.None, OnReceiveServerHeader, clientSocket);
                        } else {
                                clientSocket.BeginReceive(clientBuffer, 2 + clientBytesRead, clientMessageLength - clientBytesRead,
                                                          SocketFlags.None, OnReceiveServerMessage, clientSocket);
                        }
                } catch(Exception e) {
                        AddUnityAction(() => {
                                        UnityEngine.Debug.LogException(e, this);
                                        ServerDisconnected();
                                });
                }
        }

        private void OnConnect(IAsyncResult info) {
                try {
                        clientSocket.EndConnect(info);
                        AddUnityAction(() => {
                                        localClient.OnConnected(this);
                                        clientBytesRead = 0;
                                        clientSocket.BeginReceive(clientBuffer, 0, 2, SocketFlags.None, OnReceiveServerHeader, clientSocket);
                                });
                } catch(Exception e) {
                        var blah = localClient;
                        AddUnityAction(() => { UnityEngine.Debug.LogException(e, this); blah.OnFailedToConnect(); });
                        clientSocket = null;
                        localClient = null;
                }
        }
}
