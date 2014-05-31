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
                if(!clientIDMap.ContainsKey(client)) {
                        return;
                }
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
                                break;
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
        private bool serverNextTurn;
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

        private System.IO.FileStream replayOutput;
        private System.IO.FileStream replayInput;

        private LSNet net;
        private Server server;

        public float timeAccel = 1.0f;

        void Log(string s) {
                if(debugVomit) {
                        Debug.Log(s);
                }
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

        void OnDestroy() {
                if(currentInstance != this) {
                        return;
                }
                currentInstance = null;
                if(replayOutput != null) {
                        replayOutput.Close();
                }
                if(replayInput != null) {
                        replayInput.Close();
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

                NetworkInit();
        }

        void NetworkInit() {
                gameOver = false;
                localPlayerID = -1;
                players.Clear();
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
                if(currentInstance.replayInput != null) return;

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
        void SpawnCommand(string entityName, int team, DVector2 position, DReal rotation) {
                GameObject go = Resources.Load<GameObject>(entityName);
                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);

                Log("{" + tickID + "} Spawn " + entityName + " on team " + team + " at " + position + ":" + rotation);
                Entity thing = (Object.Instantiate(go, worldPosition, worldRotation) as GameObject).GetComponent<Entity>();
                thing.position = position;
                thing.rotation = rotation;
                thing.team = team;
                EntityCreated(thing);
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

        Entity EntityFromID(int entityID) {
                if(!worldEntities.ContainsKey(entityID)) return null;
                return worldEntities[entityID];
        }

        // (Client)
        void MoveCommand(int team, int entityID, DVector2 position) {
                var entity = EntityFromID(entityID);
                if(entity != null && entity.team == team) {
                        Log("{" + tickID + "} Move " + entity + "[" + entityID + "] to " + position);
                        entity.gameObject.SendMessage("Move", position, SendMessageOptions.DontRequireReceiver);
                }
        }

        // (Client)
        void AttackCommand(int team, int entityID, int targetID) {
                var entity = EntityFromID(entityID);
                var target = EntityFromID(targetID);
                if(entity != null && target != null && entity.team == team) {
                        Log("{" + tickID + "} " + entity + "[" + entityID + "] attack " + target + "[" + targetID + "]");
                        entity.gameObject.SendMessage("Attack", target, SendMessageOptions.DontRequireReceiver);
                }
        }

        // (Client)
        void UIActionCommand(int team, int entityID, int what) {
                var entity = EntityFromID(entityID);
                if(entity != null && entity.team == team) {
                        Log("{" + tickID + "} " + entity + "[" + entityID + "] UI action " + what);
                        entity.gameObject.SendMessage("UIAction", what, SendMessageOptions.DontRequireReceiver);
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
                                Debug.LogError("Future command on turn " + (turnID+1) + " is for turn " + onTurn + "???");
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
                                        Debug.LogWarning("Player " + p.name + " is lagging.");
                                }
                                p.unreadyTime += 1;
                                result = false;
                        }
                }
                return result;
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

        void VerifyClientGameState(int playerID, string state) {
                if(state != currentGameState) {
                        Debug.LogError("Player " + playerID + " out of sync!");
                        Debug.LogError(state);
                        Debug.LogError(currentGameState);
                }
        }

        void Update() {
                if(!worldRunning) return;

                if(replayInput != null && !goForNextTurn) {
                        // Play back commands until the next turn or the end of the replay.
                        try {
                                while(true) {
                                        var m = ProtoBuf.Serializer.DeserializeWithLengthPrefix<NetworkMessage>(replayInput, ProtoBuf.PrefixStyle.Base128);
                                        if(m == null) break;
                                        OnServerMessage(m);
                                        if(m.type == NetworkMessage.Type.NextTurn) break;
                                }
                        } catch(System.Exception e) {
                                Debug.LogException(e, this);
                        }
                }

                // FixedUpdate has an indeterminate update order, TickUpdate fixes this.
                // First the world is updated, then entities are updated in creation (id) order.
                timeSlop += Time.deltaTime * timeAccel;
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
                                        serverNextTurn = true;
                                }

                                if(goForNextTurn) {
                                        if(isHost) {
                                                currentGameState = DumpGameState();
                                                Log(currentGameState);
                                        }
                                        if(syncCheckRequested) {
                                                string state = DumpGameState();
                                                Debug.Log(state);
                                                // Make sure it doesn't exceed the maximum packet length.
                                                if(state.Length < 0xFF00) {
                                                        var m = new NetworkMessage(NetworkMessage.Type.SyncCheck);
                                                        m.gameState = state;
                                                        net.SendMessageToServer(m);
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
                                        futureQueuedCommands = tmp;
                                        goForNextTurn = false;
                                        serverNextTurn = false;
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
                if(replayInput != null) return;
                var m = new NetworkMessage(NetworkMessage.Type.Ready);
                net.SendMessageToServer(m);
        }

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

        // Locate an entity within the given circle, not on the given team.
        public static List<Entity> FindEntitiesWithinRadius(DVector2 origin, DReal radius, int ignoreTeam = -1) {
                var result = new List<Entity>();

                foreach(Entity e in currentInstance.worldEntityCache) {
                        if(e == null) continue;
                        if(e.team == ignoreTeam) continue;
                        if(e.collisionRadius == 0) continue;

                        if((e.position - origin).sqrMagnitude < radius*radius) {
                                result.Add(e);
                        }
                }

                return result;
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
                if(currentInstance.replayInput != null) return;
                if(unit == null) return;

                var m = new NetworkMessage(NetworkMessage.Type.Move);
                m.entityID = currentInstance.reverseWorldEntities[unit];
                m.position = position;
                currentInstance.net.SendMessageToServer(m);
        }

        public static void IssueAttack(Entity unit, Entity target) {
                if(currentInstance.replayInput != null) return;
                if(unit == null || target == null) return;

                var m = new NetworkMessage(NetworkMessage.Type.Attack);
                m.entityID = currentInstance.reverseWorldEntities[unit];
                m.targetID = currentInstance.reverseWorldEntities[target];
                currentInstance.net.SendMessageToServer(m);
        }

        public static void IssueUIAction(Entity unit, int what) {
                if(currentInstance.replayInput != null) return;
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
                NetworkInit();
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
                if(connectionState == ConnectionState.InGame) {
                        Debug.LogError("Player " + id + " dropped.");
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

        void SaveReplayCommand(NetworkMessage message) {
                if(replayOutput != null) {
                        ProtoBuf.Serializer.SerializeWithLengthPrefix<NetworkMessage>(replayOutput, message, ProtoBuf.PrefixStyle.Base128);
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
                        if(worldRunning) {
                                throw new System.Exception("Got start game while world running?");
                        }
                        try {
                                if(!System.IO.Directory.Exists("Replays")) {
                                        System.IO.Directory.CreateDirectory("Replays");
                                }
                                replayOutput = new System.IO.FileStream("Replays/" + System.DateTime.UtcNow.ToString("yyyy-MM-dd HH-mm-ss") + ".replay", System.IO.FileMode.CreateNew);
                        } catch(System.Exception e) {
                                Debug.LogError("Unable to record replay.");
                                Debug.LogException(e, this);
                        }
                        Application.LoadLevel(message.levelName);
                        break;
                case NetworkMessage.Type.SpawnEntity:
                        SaveReplayCommand(message);
                        QueueCommand(message.turnID, message.commandID,
                                     () => { SpawnCommand(message.entityName, message.teamID,
                                                          message.position, message.rotation); });
                        break;
                case NetworkMessage.Type.Move:
                        SaveReplayCommand(message);
                        QueueCommand(message.turnID, message.commandID,
                                     () => { MoveCommand(message.teamID, message.entityID, message.position); });
                        break;
                case NetworkMessage.Type.Attack:
                        SaveReplayCommand(message);
                        QueueCommand(message.turnID, message.commandID,
                                     () => { AttackCommand(message.teamID, message.entityID, message.targetID); });
                        break;
                case NetworkMessage.Type.UIAction:
                        SaveReplayCommand(message);
                        QueueCommand(message.turnID, message.commandID,
                                     () => { UIActionCommand(message.teamID, message.entityID, message.UIwhat); });
                        break;
                case NetworkMessage.Type.NextTurn:
                        SaveReplayCommand(message);
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
                        message.turnID = serverNextTurn ? turnID + 1 : turnID;
                        message.teamID = PlayerFromID(playerID).team;
                        net.SendMessageToAll(message);
                        break;
                case NetworkMessage.Type.Ready:
                        Log("Player " + playerID + " readyup  " + turnID + " " + tickID);
                        PlayerFromID(playerID).ready = true;
                        break;
                case NetworkMessage.Type.SyncCheck:
                        VerifyClientGameState(playerID, message.gameState);
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
                NetworkInit();
                net.Connect(address, port, this);
        }

        public void Host(int port) {
                if(connectionState != ConnectionState.Disconnected) {
                        Debug.LogError("Bad state to start a server in.");
                        return;
                }
                NetworkInit();
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

        public void PlayReplay(string path) {
                if(worldRunning) {
                        throw new System.Exception("Got start game while world running?");
                }
                replayInput = new System.IO.FileStream(path, System.IO.FileMode.Open);
                Application.LoadLevel("main"); // FIXME: Should bake this into the replay.
        }
}
