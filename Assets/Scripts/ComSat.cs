using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

class Server : IServer {
        ComSat comSat;
        LSNet net;
        int nextPlayerID = 0;

        Dictionary<NetworkClient, int> clientIDMap = new Dictionary<NetworkClient, int>();

        public Server(ComSat cs) {
                this.comSat = cs;
        }

        public void OnServerActive(LSNet net) {
                Debug.Log("Server active.");
                this.net = net;
        }
        public void OnClientConnect(NetworkClient client) {
                if(comSat.connectionState == ComSat.ConnectionState.InGame) {
                        // Nope, out you go.
                        Debug.Log("Client " + client + " attempted to join during game.");
                        net.CloseConnection(client);
                        return;
                }

                Debug.Log("Client " + client + " connected");
                // Give it a player id.
                var id = nextPlayerID;
                nextPlayerID += 1;
                clientIDMap[client] = id;

                var hello = new NetworkMessage(NetworkMessage.Type.Hello);
                hello.playerID = id;
                net.SendMessageToClient(client, hello);
                // Notify all players of the client joining.
                var newJoin = new NetworkMessage(NetworkMessage.Type.PlayerJoin);
                newJoin.playerID = id;
                net.SendMessageToAll(newJoin);

                // Send it details of other players.
                foreach(var p in comSat.players) {
                        var join = new NetworkMessage(NetworkMessage.Type.PlayerJoin);
                        join.playerID = p.id;
                        net.SendMessageToClient(client, join);
                        var update = new NetworkMessage(NetworkMessage.Type.PlayerUpdate);
                        update.playerID = p.id;
                        update.playerName = p.name;
                        update.teamID = p.team;
                        net.SendMessageToClient(client, update);
                }
        }
        public void OnClientDisconnect(NetworkClient client) {
                Debug.Log("Client " + client + " disconnected");
                int id = clientIDMap[client];
                NetworkMessage drop = new NetworkMessage(NetworkMessage.Type.PlayerLeave);
                drop.playerID = id;
                net.SendMessageToAll(drop);
                clientIDMap.Remove(client);
        }
        public void OnClientMessage(NetworkClient client, NetworkMessage message) {
                Debug.Log("Client " + client + " set message " + message);
                comSat.OnClientMessage(clientIDMap[client], message);
        }

        public void DisconnectPlayer(int id) {
                foreach(var p in clientIDMap) {
                        if(p.Value == id) {
                                net.CloseConnection(p.Key);
                                OnClientDisconnect(p.Key);
                        }
                }
        }
}

[RequireComponent (typeof(LSNet))]
public class ComSat : MonoBehaviour, IClient {
        public class Player {
                public int id;
                public string name;
                public int team;
                public bool ready;
                public int unreadyTime;
        }

        // Game runs at this rate.
        public static DReal tickRate = (DReal)1 / (DReal)25;
        // This many ticks per communication turn.
        // Max input lag = ticksPerTurn * tickRate.
        public static int ticksPerTurn = 10; // set this to 5 for release, 1 for locally debugging desyncs

        private float timeSlop;
        private int ticksRemaining; // Ticks remaining in this turn.
        private bool goForNextTurn;
        private int tickID;
        private int turnID;
        private int serverCommandID;
        private int clientCommandID;

        private int nextEntityId;
        private Dictionary<int, Entity> worldEntities;
        private Dictionary<Entity, int> reverseWorldEntities;
        private List<Entity> worldEntityCache;

        // Actions to be performed at the end of a tick.
        private List<System.Action> deferredActions;
        private List<System.Action> queuedCommands;
        private List<System.Action> futureQueuedCommands;

        private static ComSat currentInstance;

        public int localPlayerID;
        public List<Player> players = new List<Player>();

        private bool worldRunning;

        private bool syncCheckRequested;
        private bool debugVomit;

        private bool gameOver;
        private int winningTeam;

        public static int localTeam {
                get { return currentInstance.localPlayer.team; }
        }

        private System.IO.StreamWriter replayOutput;
        private int lastReplayCommandTurn;

        private LSNet net;
        private Server server;

        void Log(string s) {
                if(debugVomit) {
                        Debug.Log(s);
                }
        }

        static public void SaveReplay(string path) {
                if(currentInstance.worldRunning) {
                        throw new System.Exception("Tried to start a replay after the world began!");
                }
                currentInstance.replayOutput = new System.IO.StreamWriter(path);
        }

        void OnGUI() {
                if(!worldRunning) return;

                GUILayout.BeginArea(new Rect (Screen.width-200, 0, 200, 100));
                if(GUILayout.Button("Disconnect")) {
                        Disconnect();
                }
                if(GUILayout.Button("Check Sync")) {
                        syncCheckRequested = true;
                }
                if(GUILayout.Button(debugVomit ? "Disable verbose logging" : "Enable verbose logging")) {
                        debugVomit = !debugVomit;
                }
                if(gameOver) {
                        if(winningTeam == 0) {
                                GUILayout.Label("DRAW!");
                        } else {
                                GUILayout.Label("Player " + winningTeam + " wins!");
                        }
                }
                GUILayout.EndArea();
        }

        /*
        void OnPlayerDisconnected(NetworkPlayer player) {
                var p = SenderToPlayer(player);
                // Should kill all their units or something.
                if(p != null) {
                        networkView.RPC("PlayerDrop", RPCMode.All, p.name);
                        players.Remove(p);
                }
        }

        [RPC]
        void PlayerDrop(string name) {
                Debug.LogError("Player " + name + " has dropped!");
        }
        */

        //void OnDisconnectedFromServer() {
        //        Destroy(gameObject);
        //        Application.LoadLevel("Lobby");
        //}

        void OnDestroy() {
                if(currentInstance != this) {
                        return;
                }
                currentInstance = null;
                if(replayOutput != null) {
                        replayOutput.Close();
                }
        }

        void Awake() {
                if(currentInstance != null) {
                        Destroy(gameObject);
                        return;
                }

                DontDestroyOnLoad(gameObject);

                net = GetComponent<LSNet>();
                currentInstance = this;
                gameOver = false;
                localPlayerID = -1;

                if(Network.isServer) {
                        players = new List<Player>();
                }
        }

        void Victory(int winner) {
                gameOver = true;
                winningTeam = winner;
        }

        // Instantiate a new prefab, defering to the end of TickUpdate.
        // Prefab must be an Entity.
        public static void SpawnEntity(GameObject prefab, int team, DVector2 position, DReal rotation) {
                currentInstance.deferredActions.Add(() => {
                                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);
                                Entity thing = (Object.Instantiate(prefab, worldPosition, worldRotation) as GameObject).GetComponent<Entity>();
                                thing.position = position;
                                thing.rotation = rotation;
                                thing.team = team;
                                currentInstance.EntityCreated(thing);
                        });
        }

        public static void SpawnEntity(Entity origin, GameObject prefab, DVector2 position, DReal rotation) {
                currentInstance.deferredActions.Add(() => {
                                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);
                                Entity thing = (Object.Instantiate(prefab, worldPosition, worldRotation) as GameObject).GetComponent<Entity>();
                                thing.position = position;
                                thing.rotation = rotation;
                                thing.team = origin.team;
                                thing.origin = origin;
                                currentInstance.EntityCreated(thing);
                        });
        }

        public static void SpawnEntity(Entity origin, GameObject prefab, DVector2 position, DReal rotation, System.Action<Entity> onSpawn) {
                currentInstance.deferredActions.Add(() => {
                                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);
                                Entity thing = (Object.Instantiate(prefab, worldPosition, worldRotation) as GameObject).GetComponent<Entity>();
                                thing.position = position;
                                thing.rotation = rotation;
                                thing.team = origin.team;
                                thing.origin = origin;
                                currentInstance.EntityCreated(thing);
                                onSpawn(thing);
                        });
        }

        // Create a new entity at whereever.
        // Called by all, but ignored everywhere but the server.
        public static void Spawn(string entityName, int team, DVector2 position, DReal rotation) {
                if(team != 0) {
                        // Only spawn if the team exists.
                        bool ok = false;

                        foreach(var p in currentInstance.players) {
                                if(p.team == team) {
                                        ok = true;
                                        break;
                                }
                        }

                        if(!ok) return;
                }

                var m = new NetworkMessage(NetworkMessage.Type.SpawnEntity);
                m.entityName = entityName;
                m.teamID = team;
                m.position = position;
                m.rotation = rotation;
                currentInstance.net.SendMessageToServer(m);
        }

        // (Client)
        void SpawnCommand(int onTurn, int commandID, string entityName, int team, DVector2 position, DReal rotation) {
                GameObject go = Resources.Load<GameObject>(entityName);
                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);

                QueueCommand(onTurn, commandID, () => {
                                SaveReplayCommand(onTurn, "S " + entityName + " " + team + " " + position + " " + rotation);
                                Log("{" + tickID + "} Spawn " + entityName + " on team " + team + " at " + position + ":" + rotation);
                                Entity thing = (Object.Instantiate(go, worldPosition, worldRotation) as GameObject).GetComponent<Entity>();
                                thing.position = position;
                                thing.rotation = rotation;
                                thing.team = team;
                                EntityCreated(thing);
                        });
        }

        void EntityCreated(Entity ent) {
                int id = currentInstance.nextEntityId;
                currentInstance.nextEntityId += 1;
                currentInstance.worldEntities[id] = ent;
                currentInstance.reverseWorldEntities[ent] = id;
                currentInstance.worldEntityCache.Add(ent);
                Log("{" + tickID + "} Created entity " + ent + "[" + id + "] at " + ent.position + ":" + ent.rotation);
        }

        public static void DestroyEntity(Entity e) {
                currentInstance.deferredActions.Add(() => {
                                if(!currentInstance.reverseWorldEntities.ContainsKey(e)) {
                                        // It's possible for a thing to be destroyed twice in one tick.
                                        return;
                                }
                                int id = currentInstance.reverseWorldEntities[e];
                                currentInstance.Log("{" + currentInstance.tickID + "} Destroy entity " + e + "[" + id + "] at " + e.position + ":" + e.rotation);
                                currentInstance.worldEntities.Remove(id);
                                currentInstance.reverseWorldEntities.Remove(e);
                                currentInstance.worldEntityCache.Remove(e);
                                Object.Destroy(e.gameObject);
                        });
        }

        // (Client)
        void MoveCommand(int onTurn, int commandID, int team, int entityID, DVector2 position) {
                var entity = worldEntities[entityID];
                Log("Got move action on " + turnID + "@" + tickID + " for turn " + onTurn);
                QueueCommand(onTurn, commandID, () => {
                                SaveReplayCommand(onTurn, "M " + entityID + " " + position);
                                if(entity != null && entity.team == team) {
                                        Log("{" + tickID + "} Move " + entity + "[" + entityID + "] to " + position);
                                        entity.gameObject.SendMessage("Move", position, SendMessageOptions.DontRequireReceiver);
                                }
                        });
        }

        // (Client)
        void AttackCommand(int onTurn, int commandID, int team, int entityID, int targetID) {
                var entity = worldEntities[entityID];
                var target = worldEntities[targetID];
                QueueCommand(onTurn, commandID, () => {
                                SaveReplayCommand(onTurn, "A " + entityID + " " + targetID);
                                if(entity != null && target != null && entity.team == team) {
                                        Log("{" + tickID + "} " + entity + "[" + entityID + "] attack " + target + "[" + targetID + "]");
                                        entity.gameObject.SendMessage("Attack", target, SendMessageOptions.DontRequireReceiver);
                                }
                        });
        }

        // (Client)
        void UIActionCommand(int onTurn, int commandID, int team, int entityID, int what) {
                var entity = worldEntities[entityID];
                QueueCommand(onTurn, commandID, () => {
                                SaveReplayCommand(onTurn, "U " + entityID + " " + what);
                                if(entity != null && entity.team == team) {
                                        Log("{" + tickID + "} " + entity + "[" + entityID + "] UI action " + what);
                                        entity.gameObject.SendMessage("UIAction", what, SendMessageOptions.DontRequireReceiver);
                                }
                        });
        }

        void SaveReplayCommand(int onTurn, string command) {
                lastReplayCommandTurn = onTurn;
                if(replayOutput != null) {
                        replayOutput.WriteLine(onTurn + " " + command);
                }
        }

        // (Client)
        void QueueCommand(int onTurn, int commandID, System.Action command) {
                clientCommandID += 1;
                if(commandID != clientCommandID) {
                        Debug.LogError("Command out of order.");
                }
                if(goForNextTurn) {
                        if(onTurn != turnID+1) {
                                Debug.LogError("Future command on turn " + turnID+1 + " is for turn " + onTurn + "???");
                        }
                        futureQueuedCommands.Add(command);
                } else {
                        if(onTurn != turnID) {
                                Debug.LogError("Command on turn " + turnID + " is for turn " + onTurn + "???");
                        }
                        queuedCommands.Add(command);
                }
        }

        private static uint randomValue;

        void OnLevelWasLoaded(int level) {
                if(level == 0) { // lobby.
                        worldRunning = false;
                        return;
                }
                worldRunning = true;

                timeSlop = 0.0f;
                ticksRemaining = 0;
                nextEntityId = 1;
                randomValue = 42;
                goForNextTurn = false;
                tickID = 0;
                turnID = 0;

                worldEntities = new Dictionary<int, Entity>();
                reverseWorldEntities = new Dictionary<Entity, int>();
                worldEntityCache = new List<Entity>(); // Faster to iterate through.
                deferredActions = new List<System.Action>();
                queuedCommands = new List<System.Action>();
                futureQueuedCommands = new List<System.Action>();

                if(isHost) {
                        ClearReady();
                }

                ReadyUp();
        }

        string DumpGameState() {
                string result = "Tick " + tickID + "; Turn " + turnID + "\n";
                foreach(var e in worldEntityCache) {
                        if(e != null) {
                                result += "Ent " + e + "[" + reverseWorldEntities[e] + "] " + e.position + ":" + e.rotation + "\n";
                        }
                }

                return result;
        }

        // (Server)
        void ClearReady() {
                foreach(var p in players) {
                        p.ready = false;
                        p.unreadyTime = 0;
                }
        }

        // (Server)
        bool EveryoneIsReady() {
                bool result = true;
                foreach(var p in players) {
                        if(!p.ready) {
                                if(p.unreadyTime > 1) {
                                        //networkView.RPC("LagWarning", RPCMode.All, p.name);
                                }
                                p.unreadyTime += 1;
                                result = false;
                        }
                }
                return result;
        }

        [RPC]
        void LagWarning(string name) {
                Debug.LogError("Player " + name + " is lagging.");
        }

        // Called every tickRate seconds when the world is live.
        // (Client)
        void TickUpdate() {
                // Must tick all objects in a consistent order across machines.
                foreach(Entity e in worldEntityCache) {
                        if(e != null) {
                                e.TickUpdate();
                        }
                }

                foreach(var a in deferredActions) {
                        a();
                }
                deferredActions.Clear();
        }

        string currentGameState;

        /*
          [RPC]
          void VerifyGameState(string state, NetworkMessageInfo info) {
          if(!Network.isServer) return; // ????
          if(state.Length != 4096 && currentGameState != state) {
          Debug.LogError("Client " + SenderToPlayer(info.sender) + " out of sync!");
          Debug.LogError(state);
          Debug.LogError(currentGameState);
          networkView.RPC("SyncStateNotify", RPCMode.All, false, SenderToPlayer(info.sender).team);
          }/* else {
          networkView.RPC("SyncStateNotify", RPCMode.All, true, SenderToPlayer(info.sender).team);
          }*/ /*
                }

                [RPC]
                void SyncStateNotify(bool ok, int player) {
                if(ok) {
                Debug.LogError("Player " + player + " has good sync.");
                } else {
                Debug.LogError("Player " + player + " desynced!");
                }
                }
              */

        void Update() {
                if(!worldRunning) return;

                // FixedUpdate has an indeterminate update order, TickUpdate fixes this.
                // First the world is updated, then entities are updated in creation (id) order.
                timeSlop += Time.deltaTime;
                while(timeSlop >= (float)tickRate) {
                        timeSlop -= (float)tickRate;
                        if(ticksRemaining != 0) {
                                Log("Tick " + tickID);
                                tickID += 1;
                                TickUpdate();
                                ticksRemaining -= 1;
                        }
                        if(ticksRemaining == 0) {
                                if(isHost && EveryoneIsReady()) {
                                        // Advance the game turn.
                                        var m = new NetworkMessage(NetworkMessage.Type.NextTurn);
                                        net.SendMessageToAll(m);
                                        ClearReady();
                                }

                                if(goForNextTurn) {
                                        if(isHost) {
                                                currentGameState = DumpGameState();
                                                Log(currentGameState);
                                        }
                                        if(syncCheckRequested) {
                                                string state = DumpGameState();
                                                if(state.Length < 4096) {
                                                        //Debug.Log(state);
                                                        networkView.RPC("VerifyGameState", RPCMode.Server, state);
                                                }
                                                syncCheckRequested = false;
                                        }

                                        Log("Advancing turn. " + turnID + " on tick " + tickID);
                                        foreach(System.Action a in queuedCommands) {
                                                Log("{" + tickID + "} Issue queued command " + a);
                                                a();
                                        }
                                        queuedCommands.Clear();
                                        var tmp = queuedCommands;
                                        queuedCommands = futureQueuedCommands;
                                        if(replayOutput != null && lastReplayCommandTurn == turnID) {
                                                replayOutput.WriteLine(turnID + " T");
                                        }
                                        futureQueuedCommands = tmp;
                                        goForNextTurn = false;
                                        ReadyUp();
                                        ticksRemaining = ticksPerTurn;
                                        turnID += 1;
                                }
                        }
                }

                worldEntityCache.RemoveAll((Entity e) => { return e == null; });

                if(!gameOver && turnID > 5) {
                        // Win check.
                        int winningTeam = 0;
                        int teamMask = 0;
                        foreach(var ent in worldEntityCache) {
                                if(ent.team == 0) continue;
                                teamMask |= 1 << ent.team;
                                winningTeam = ent.team;
                        }

                        // Magic bit hackery to check if exactly one bit is set (power of two).
                        // No bits is also a possiblilty.
                        if(teamMask == 0 || (teamMask & (teamMask - 1)) == 0) {
                                Victory(winningTeam);
                        }
                }
        }

        void ReadyUp() {
                var m = new NetworkMessage(NetworkMessage.Type.Ready);
                net.SendMessageToServer(m);
        }

        /*
        // Mark a client as ready. (Server)
        [RPC]
        void DoReadyUp(NetworkMessageInfo info) {
        foreach(var p in players) {
        if(p.client == info.sender) {
        Log("Player " + p.id + " readyup  " + turnID + " " + tickID);
        p.ready = true;
        return;
        }
        }
        Debug.LogWarning("ReadyUp from unknown client " + info);
        }
        */

        // Advance to the next communication turn. (Client)
        void NextTurn() {
                Log("Next turn.  " + turnID + " " + tickID);
                if(goForNextTurn) {
                        Debug.LogError("Duplicate NextTurn call!");
                }
                goForNextTurn = true;
        }

        Player PlayerFromID(int id) {
                foreach(var p in players) {
                        if(p.id == id) {
                                return p;
                        }
                }
                return null;
        }

        // Cast a line from A to B, checking for collisions with other entities.
        public static Entity LineCast(DVector2 start, DVector2 end, out DVector2 hitPosition, int ignoreTeam = -1) {
                foreach(Entity e in currentInstance.worldEntityCache) {
                        if(e == null) continue;
                        if(e.team == ignoreTeam) continue;
                        if(e.collisionRadius == 0) continue;

                        DVector2 result;
                        if(Utility.IntersectLineCircle(e.position, e.collisionRadius, start, end, out result)) {
                                //Debug.Log("Cast line " + start + "-" + end + " hit " + e + " at " + result);
                                hitPosition = result;
                                return e;
                        }
                }
                hitPosition = new DVector2();

                return null;
        }

        // Locate an entity within the given circle, not on the given team.
        public static Entity FindEntityWithinRadius(DVector2 origin, DReal radius, int ignoreTeam = -1) {
                foreach(Entity e in currentInstance.worldEntityCache) {
                        if(e == null) continue;
                        if(e.team == ignoreTeam) continue;
                        if(e.collisionRadius == 0) continue;

                        if((e.position - origin).sqrMagnitude < radius*radius) {
                                return e;
                        }
                }

                return null;
        }

        public static DReal RandomValue() {
                randomValue = 22695477 * randomValue + 1;
                return randomValue;
        }

        public static DReal RandomRange(DReal min, DReal max) {
                var range = DReal.Max(0, max - min);
                var n = RandomValue() / (uint)0xFFFFFFFF;
                return n * range + min;
        }

        // Game UI commands.
        public static void IssueMove(Entity unit, DVector2 position) {
                if(unit == null) return;

                var m = new NetworkMessage(NetworkMessage.Type.Move);
                m.entityID = currentInstance.reverseWorldEntities[unit];
                m.position = position;
                currentInstance.net.SendMessageToServer(m);
        }

        public static void IssueAttack(Entity unit, Entity target) {
                if(unit == null || target == null) return;

                var m = new NetworkMessage(NetworkMessage.Type.Attack);
                m.entityID = currentInstance.reverseWorldEntities[unit];
                m.targetID = currentInstance.reverseWorldEntities[target];
                currentInstance.net.SendMessageToServer(m);
        }

        public static void IssueUIAction(Entity unit, int what) {
                if(unit == null) return;

                var m = new NetworkMessage(NetworkMessage.Type.UIAction);
                m.entityID = currentInstance.reverseWorldEntities[unit];
                m.UIwhat = what;
                currentInstance.net.SendMessageToServer(m);
        }

        // Network stuff.

        public void OnConnected(LSNet net) {
                Debug.Log("Connected to server.");
        }
        public void OnDisconnected() {
                Debug.Log("Disconnected from server.");
                if(connectionState == ConnectionState.InGame) {
                        Application.LoadLevel("Lobby");
                }
        }
        public void OnFailedToConnect() {
                Debug.Log("Unabled to connect to server.");
        }

        void PlayerJoin(int id) {
                if(connectionState == ConnectionState.InGame) {
                        Debug.LogWarning("Player joining while in game?");
                }
                var p = new Player();
                p.id = id;
                p.name = "<Player " + p.id + ">";
                p.team = 0;
                if(isHost) {
                        // Automatically assign a team.
                        var teams = new List<int>{ 1,2,3,4,5,6,7 };
                        foreach(var player in players) {
                                teams.Remove(player.team);
                        }
                        if(teams.Count != 0) {
                                p.team = teams[0];
                        }
                        // Blah blah.
                        var update = new NetworkMessage(NetworkMessage.Type.PlayerUpdate);
                        update.playerID = p.id;
                        update.teamID = p.team;
                        net.SendMessageToAll(update);
                }
                players.Add(p);
        }

        void PlayerLeave(int id) {
                if(id == localPlayerID) {
                        Debug.LogWarning("Saw myself leave?");
                }
                players.RemoveAll(p => p.id == id);
        }

        void PlayerUpdate(int id, string name, int team) {
                var p = PlayerFromID(id);
                if(name.Length != 0) {
                        p.name = name;
                }
                if(team != -1) {
                        p.team = team;
                }
        }

        public void OnServerMessage(NetworkMessage message) {
                Debug.Log("Server sent message " + message + " " + message.type);
                switch(message.type) {
                case NetworkMessage.Type.Hello:
                        localPlayerID = message.playerID;
                        break;
                case NetworkMessage.Type.PlayerJoin:
                        PlayerJoin(message.playerID);
                        break;
                case NetworkMessage.Type.PlayerLeave:
                        PlayerLeave(message.playerID);
                        break;
                case NetworkMessage.Type.PlayerUpdate:
                        PlayerUpdate(message.playerID, message.playerName, message.teamID);
                        break;
                case NetworkMessage.Type.StartGame:
                        Application.LoadLevel(message.levelName);
                        break;
                case NetworkMessage.Type.SpawnEntity:
                        SpawnCommand(message.turnID, message.commandID,
                                     message.entityName, message.teamID,
                                     message.position, message.rotation);
                        break;
                case NetworkMessage.Type.Move:
                        MoveCommand(message.turnID, message.commandID,
                                    message.teamID, message.entityID, message.position);
                        break;
                case NetworkMessage.Type.Attack:
                        AttackCommand(message.turnID, message.commandID,
                                      message.teamID, message.entityID, message.targetID);
                        break;
                case NetworkMessage.Type.UIAction:
                        UIActionCommand(message.turnID, message.commandID,
                                        message.teamID, message.entityID, message.UIwhat);
                        break;
                case NetworkMessage.Type.NextTurn:
                        NextTurn();
                        break;
                default:
                        Debug.Log("Bad server message " + message.type);
                        break;
                }
        }

        bool PlayerIsAdmin(int id) {
                return id == localPlayerID;
        }

        public void OnClientMessage(int playerID, NetworkMessage message) {
                Debug.Log("Player " + playerID + " sent message " + message + " " + message.type);
                switch(message.type) {
                case NetworkMessage.Type.PlayerUpdate:
                        if(PlayerIsAdmin(playerID) || playerID == message.playerID) {
                                net.SendMessageToAll(message);
                        }
                        break;
                case NetworkMessage.Type.KickPlayer:
                        if(PlayerIsAdmin(playerID) && !PlayerIsAdmin(message.playerID)) {
                                server.DisconnectPlayer(message.playerID);
                        }
                        break;
                case NetworkMessage.Type.StartGame:
                        if(PlayerIsAdmin(playerID)) {
                                net.SendMessageToAll(message);
                        }
                        break;
                case NetworkMessage.Type.SpawnEntity:
                        if(PlayerIsAdmin(playerID)) {
                                serverCommandID += 1;
                                message.commandID = serverCommandID;
                                message.turnID = goForNextTurn ? turnID + 1 : turnID;
                                net.SendMessageToAll(message);
                        }
                        break;
                case NetworkMessage.Type.Move:
                case NetworkMessage.Type.Attack:
                case NetworkMessage.Type.UIAction:
                        serverCommandID += 1;
                        message.commandID = serverCommandID;
                        message.turnID = goForNextTurn ? turnID + 1 : turnID;
                        message.teamID = PlayerFromID(playerID).team;
                        net.SendMessageToAll(message);
                        break;
                case NetworkMessage.Type.Ready:
                        Log("Player " + playerID + " readyup  " + turnID + " " + tickID);
                        PlayerFromID(playerID).ready = true;
                        break;
                default:
                        Debug.Log("Bad client message " + message.type);
                        break;
                }
        }

        public enum ConnectionState {
                Disconnected, Connecting, Lobby, InGame
        }
        public bool isHost {
                get {
                        return server != null;
                }
        }
        public Player localPlayer {
                get {
                        return PlayerFromID(localPlayerID);
                }
        }

        public ConnectionState connectionState {
                get {
                        if(worldRunning) {
                                return ConnectionState.InGame;
                        } else if(localPlayerID != -1) {
                                return ConnectionState.Lobby;
                        } else if(net.isServer || net.isClient) {
                                return ConnectionState.Connecting;
                        } else {
                                return ConnectionState.Disconnected;
                        }
                }
        }

        // UI commands.

        public void Connect(string address, int port) {
                if(connectionState != ConnectionState.Disconnected) {
                        Debug.LogError("Bad state to connect in.");
                        return;
                }
                localPlayerID = -1;
                net.Connect(address, port, this);
        }

        public void Host(int port) {
                if(connectionState != ConnectionState.Disconnected) {
                        Debug.LogError("Bad state to start a server in.");
                        return;
                }
                localPlayerID = -1;
                server = new Server(this);
                net.InitializeServer(port, server, this);
        }

        public void Disconnect() {
                // Fuck it, just blow everything away.
                // Would be nice to return to the lobby and stay connected to the server
                // when leaving the game.
                net.Disconnect();
                Destroy(gameObject);
                Application.LoadLevel("Lobby");
        }
        public void StartGame() {
                var m = new NetworkMessage(NetworkMessage.Type.StartGame);
                m.levelName = "main";
                net.SendMessageToServer(m);
        }
        public void SetPlayerName(Player player, string name) {
                var m = new NetworkMessage(NetworkMessage.Type.PlayerUpdate);
                m.playerID = player.id;
                m.playerName = name;
                net.SendMessageToServer(m);
        }
        public void SetPlayerTeam(Player player, int team) {
                var m = new NetworkMessage(NetworkMessage.Type.PlayerUpdate);
                m.playerID = player.id;
                m.teamID = team;
                net.SendMessageToServer(m);
        }
        public void Kick(Player player) {
                var m = new NetworkMessage(NetworkMessage.Type.KickPlayer);
                m.playerID = player.id;
                net.SendMessageToServer(m);
        }
}
