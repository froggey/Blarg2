using UnityEngine;
using System.Collections.Generic;

public class Lobby : MonoBehaviour {
        class Player {
                public int id;
                public string name;
                public NetworkPlayer client;

                public Player(int id, string name, NetworkPlayer client) {
                        this.id = id;
                        this.name = name;
                        this.client = client;
                }

                public Player(int id, string name) {
                        this.id = id;
                        this.name = name;
                }
        }

        public static string localPlayerName = "";
        public static string hostAddress = "";
        public static string hostPort = "11235";

        private bool connectionInProgress;

        private NetworkConnectionError lastError;

        private int myPlayerId;
        private int nextPlayerId;
        private List<Player> players;

        //private GameObject remoteClientPrefab;
        //private GameObject localClientPrefab;
        //private GameObject serverPrefab;

        void Start() {
                connectionInProgress = false;
                lastError = NetworkConnectionError.NoError;
        }

        void OnGUI() {
                if (Network.isServer) {
                        HostGUI();
                } else if (Network.isClient) {
                        ClientGUI();
                } else {
                        ConnectGUI();
                }
        }

        void ConnectGUI() {
                if(connectionInProgress) {
                        GUILayout.BeginArea(new Rect (100, 50, Screen.width-200, Screen.height-100));
                        GUILayout.Label("Connecting to " + hostAddress + ":" + hostPort);
                        if(GUILayout.Button("Abort")) {
                                Network.Disconnect(0);
                                connectionInProgress = false;
                        }
                        GUILayout.EndArea();
                        return;
                }
		GUILayout.BeginArea(new Rect (100, 50, Screen.width-200, Screen.height-100));
                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Player Name: ");
                localPlayerName = GUILayout.TextField(localPlayerName, 40);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server Address: ");
                hostAddress = GUILayout.TextField(hostAddress, 40);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Server port: ");
                hostPort = GUILayout.TextField(hostPort, 40);
                GUILayout.EndHorizontal();

		if(GUILayout.Button("Connect")) {
                        lastError = Network.Connect(hostAddress, System.Convert.ToInt32(hostPort));
                        if(lastError != NetworkConnectionError.NoError) {
                                print("Connection error: " + lastError);
                        } else {
                                connectionInProgress = true;
                        }
                }
		if(GUILayout.Button("Host")) {
                        Network.InitializeServer(32, System.Convert.ToInt32(hostPort), false);
                }

                if(lastError != NetworkConnectionError.NoError) {
                        GUILayout.Label("Last error: " + lastError);
                }
                GUILayout.EndVertical();
		GUILayout.EndArea();
        }

        void OnFailedToConnect(NetworkConnectionError error) {
                connectionInProgress = false;
                print("Connection error: " + error);
                lastError = error;
        }

        void ClientGUI() {
		GUILayout.BeginArea(new Rect (100, 50, Screen.width-200, Screen.height-100));
                GUILayout.BeginVertical();

                foreach(var player in players) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(player.name);
                        GUILayout.EndHorizontal();
                }

		if(GUILayout.Button("Disconnect")) {
                        Network.Disconnect();
                }

                GUILayout.EndVertical();
		GUILayout.EndArea();
        }

        void HostGUI() {
		GUILayout.BeginArea(new Rect (100, 50, Screen.width-200, Screen.height-100));
                GUILayout.BeginVertical();

                foreach(var player in players) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(player.name);
                        if(player.id != 0 && GUILayout.Button("Kick")) {
                                Network.CloseConnection(player.client, true);
                        }
                        GUILayout.EndHorizontal();
                }

		if(GUILayout.Button("Start Game")) {
                        StartGame();
                }
		if(GUILayout.Button("Exit")) {
                        Network.Disconnect();
                }

                GUILayout.EndVertical();
		GUILayout.EndArea();
        }

        private bool gameStarting;

        public GameObject comSatPrefab;

        void StartGame() {
                gameStarting = true;
                int nextTeam = 1;
                var comSat = (Network.Instantiate(comSatPrefab, new Vector3(), new Quaternion(), 0) as GameObject).GetComponent<ComSat>();

                foreach(var p in players) {
                        comSat.ServerAddPlayer(p.id, p.client, p.name, nextTeam);
                        nextTeam += 1;
                }

                networkView.RPC("BeginGame", RPCMode.AllBuffered, "main"); // level name
        }

        [RPC]
        void BeginGame(string levelName) {
                Application.LoadLevel(levelName);
        }

        void OnConnectedToServer() {
                connectionInProgress = false;
                myPlayerId = -1;
                players = new List<Player>();
                networkView.RPC("Hello", RPCMode.Server, localPlayerName);
        }

        void OnPlayerConnected(NetworkPlayer player) {
                print("Player connected " + player.guid);

                if(gameStarting) {
                        Network.CloseConnection(player, true);
                }
        }

        void OnPlayerDisconnected(NetworkPlayer player) {
                print("Player disconnected " + player.guid);

                foreach(var p in players) {
                        if(p.client == player) {
                                networkView.RPC("ClientDisconnected", RPCMode.AllBuffered, p.id);
                                return;
                        }
                }
        }

        void OnServerInitialized() {
                print("Server ready for action!");
                players = new List<Player>();
                nextPlayerId = 1;
                myPlayerId = 0;
                gameStarting = false;

                var player = new Player(0, localPlayerName);
                players.Add(player);
        }

        [RPC]
        void Hello(string clientName, NetworkMessageInfo info) {
                print("Client " + info + " says hello, named " + clientName);
                foreach(var player in players) {
                        if(player.name == clientName) {
                                print("Client nickname in use.");
                                Network.CloseConnection(info.sender, true);
                                return;
                        }
                }
                foreach(var player in players) {
                        networkView.RPC("ClientConnected", info.sender, player.id, player.name);
                }

                networkView.RPC("ClientConnected", RPCMode.OthersBuffered, nextPlayerId, clientName);
                players.Add(new Player(nextPlayerId, clientName, info.sender));
                nextPlayerId += 1;
       }

        [RPC]
        void ClientConnected(int playerId, string name) {
                var player = new Player(nextPlayerId, name);
                players.Add(player);

                if(name == localPlayerName) {
                        // here I am!
                        myPlayerId = playerId;
                }
        }

        [RPC]
        void ClientDisconnected(int playerId) {
                players.RemoveAll((Player player) => { return player.id == playerId; });
        }
}
