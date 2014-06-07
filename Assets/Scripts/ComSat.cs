using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

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
                public string state;
        }

        // Game runs at this rate.
        public static DReal tickRate = (DReal)1 / (DReal)25;
        // This many ticks per communication turn.
        // Max input lag = ticksPerTurn * tickRate.
        public static int ticksPerTurn = 10; // set this to 10(?) for release, 1 for locally debugging desyncs

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
        private List<Entity> worldEntityCollisionCache;

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
                get { return currentInstance.localPlayer == null ? -1 : currentInstance.localPlayer.team; }
        }

        private System.IO.FileStream replayOutput;
        private System.IO.FileStream replayInput;

        private LSNet net;
        private Server server;

        public float timeAccel = 1.0f;

        // Dump the entire gamestate every turn.
        public bool fullDump;
        // Trace actions and stuff.
        public bool enableActionTracing;
        // Sync check every tick.
        public bool enableContinuousSyncCheck;

        private float avgTickTime;
        private int nCreatedThisTick;
        private float avgCreatedPerTick;
        private int nDestroyedThisTick;
        private float avgDestroyedPerTick;


        void Log(string s) {
                if(debugVomit) {
                        Debug.Log(s);
                }
        }

        void OnGUI() {
                if(!worldRunning) return;

                GUILayout.BeginArea(new Rect (Screen.width-200, 0, 200, 400));
                if(GUILayout.Button("Disconnect")) {
                        Disconnect();
                }
                if(GUILayout.Button("Check Sync")) {
                        syncCheckRequested = true;
                }
                if(GUILayout.Button(debugVomit ? "Disable verbose logging" : "Enable verbose logging")) {
                        debugVomit = !debugVomit;
                }
                GUILayout.Label("Avg tick time: " + (avgTickTime * 1000).ToString("n") + "ms");
                GUILayout.Label("Avg instantiations: " + avgCreatedPerTick.ToString("n"));
                GUILayout.Label("Avg destructions: " + avgDestroyedPerTick.ToString("n"));
                GUILayout.Label("Object pools: ");
                foreach(var kv in ObjectPool.Pools) {
                        GUILayout.Label(kv.Key.name + " " + kv.Value.Count);
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
                currentInstance.SpawnEntity(prefab, null, team, position, rotation, e => {});
        }

        public static void SpawnEntity(Entity origin, GameObject prefab, DVector2 position, DReal rotation) {
                currentInstance.SpawnEntity(prefab, origin, origin == null ? 0 : origin.team, position, rotation, e => {});
        }

        public static void SpawnEntity(Entity origin, GameObject prefab, DVector2 position, DReal rotation, System.Action<Entity> onSpawn) {
                currentInstance.SpawnEntity(prefab, origin, origin == null ? 0 : origin.team, position, rotation, onSpawn);
        }

        public ResourceSet[] teamResources;
        public static ResourceSet localTeamResources { get { return currentInstance.teamResources[localTeam]; } }

        public static void AddResource(int team, ResourceType resource, int amount) {
                switch (resource) {
                        case ResourceType.MagicSmoke:
                                currentInstance.teamResources[team].MagicSmoke += amount;
                                break;
                        case ResourceType.Metal:
                                currentInstance.teamResources[team].Metal += amount;
                                break;
                }
        }

        public static bool TakeResources(int team, ResourceSet resources) {
                var rs = currentInstance.teamResources[team];
                if (rs.ContainsAtLeast(resources)) {
                        rs.Metal -= resources.Metal;
                        rs.MagicSmoke -= resources.MagicSmoke;
                        return true;
                }
                else {
                        return false;
                }
        }

        // Create a new entity at whereever.
        // Called by all, but ignored everywhere but the server.
        public static void Spawn(string entityName, int team, DVector2 position, DReal rotation) {
                if(currentInstance.replayInput != null) return;

                // Only spawn if the team exists or if generating scenery.
                if(team != 0 && !currentInstance.players.Any(p => p.team == team)) {
                        return;
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
                Log("{" + tickID + "} Spawn " + entityName + " on team " + team + " at " + position + ":" + rotation);
                GameObject go = Resources.Load<GameObject>(entityName);
                SpawnEntity(go, null, team, position, rotation, e => {});
        }

        void SpawnEntity(GameObject prefab, Entity origin, int team, DVector2 position, DReal rotation, System.Action<Entity> onSpawn) {
                if(debugVomit) {
                        Log("Spawn entity " + prefab + " from " + origin + " on team " + team + " at " + position + ":" + rotation);
                }
                deferredActions.Add(() => {
                                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);

                                var obj = prefab.GetComponent<PooledObject>() != null
                                          ? ObjectPool.For(prefab).Instantiate(worldPosition, worldRotation)
                                          : GameObject.Instantiate(prefab, worldPosition, worldRotation) as GameObject;
                                var thing = obj.GetComponent<Entity>();

                                int id = nextEntityId;
                                nextEntityId += 1;

                                thing.position = position;
                                thing.rotation = rotation;
                                thing.team = team;
                                thing.origin = origin;

                                worldEntities[id] = thing;
                                reverseWorldEntities[thing] = id;
                                worldEntityCache.Add(thing);
                                if(thing.collisionRadius != 0) {
                                        worldEntityCollisionCache.Add(thing);
                                }
                                thing.OnInstantiate();
                                onSpawn(thing);

                                nCreatedThisTick += 1;
                                if(debugVomit) {
                                        Log("{" + tickID + "} Created entity " + thing + "[" + id + "] at " + position + ":" + rotation);
                                }
                        });
        }

        public static void DestroyEntity(Entity e) {
                if(currentInstance.debugVomit) {
                        currentInstance.Log("Destroy entity " + e + "[" + currentInstance.reverseWorldEntities[e] + "] at " + e.position + ":" + e.rotation);
                }
                currentInstance.deferredActions.Add(() => {
                                if(!currentInstance.reverseWorldEntities.ContainsKey(e)) {
                                        // It's possible for a thing to be destroyed twice in one tick.
                                        return;
                                }
                                int id = currentInstance.reverseWorldEntities[e];
                                if(currentInstance.debugVomit) {
                                        currentInstance.Log("{" + currentInstance.tickID + "} Destroy entity " + e + "[" + id + "] at " + e.position + ":" + e.rotation);
                                }
                                currentInstance.worldEntities.Remove(id);
                                currentInstance.reverseWorldEntities.Remove(e);
                                currentInstance.worldEntityCache.Remove(e);
                                currentInstance.worldEntityCollisionCache.Remove(e);

                                if(e.GetComponent<PooledObject>() != null) {
                                        ObjectPool.For(e.GetComponent<PooledObject>().prototype).Uninstantiate(e.gameObject);
                                } else {
                                        GameObject.Destroy(e.gameObject);
                                }

                                currentInstance.nDestroyedThisTick += 1;
                        });
        }

        public static bool EntityExists(Entity e) {
                return e != null && currentInstance.reverseWorldEntities.ContainsKey(e);
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
                        if(replayOutput != null) {
                                replayOutput.Close();
                                replayOutput = null;
                        }
                        if(replayInput != null) {
                                replayInput.Close();
                                replayInput = null;
                        }
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

                serverNextTurn = false;
                serverCommandID = 0;
                clientCommandID = 0;

                gameOver = false;

                avgTickTime = 0.0f;
                avgCreatedPerTick = 0.0f;
                avgDestroyedPerTick = 0.0f;

                worldEntities = new Dictionary<int, Entity>();
                reverseWorldEntities = new Dictionary<Entity, int>();
                worldEntityCache = new List<Entity>(); // Faster to iterate through.
                worldEntityCollisionCache = new List<Entity>(); // Faster to iterate through.
                deferredActions = new List<System.Action>();
                queuedCommands = new List<System.Action>();
                futureQueuedCommands = new List<System.Action>();

                ObjectPool.FlushAll();

                if(isHost) {
                        ClearReady();
                }

                ReadyUp(null);
        }

        string DumpGameState() {
                string result = "Tick " + tickID + "; Turn " + turnID + "\n";
                foreach(var e in worldEntityCache) {
                        result += "Ent " + e + "[" + reverseWorldEntities[e] + "] " + e.position + ":" + e.rotation + "\n";
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
                        e.TickUpdate();
                }

                foreach(var a in deferredActions) {
                        a();
                }
                deferredActions.Clear();
        }

        string currentGameState;

        void VerifyClientGameState(int playerID, string state) {
                if(state != currentGameState) {
                        Debug.LogError("Player " + playerID + " " + PlayerFromID(playerID).name + " out of sync!");
                        Debug.LogError(state);
                        Debug.LogError(currentGameState);
                } else {
                        Debug.Log("Player " + playerID + " " + PlayerFromID(playerID).name + " in sync.");
                }
        }

        int thingsDoneThisFrame;

        public static bool RateLimit() {
                if(currentInstance.thingsDoneThisFrame > 10) {
                        return false;
                }
                currentInstance.thingsDoneThisFrame += 1;
                return true;
        }

        void Update() {
                if(!worldRunning) return;

                thingsDoneThisFrame = 0;

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
                                var sw = new System.Diagnostics.Stopwatch();
                                sw.Start();
                                nCreatedThisTick = 0;
                                nDestroyedThisTick = 0;
                                TickUpdate();
                                sw.Stop();
                                avgTickTime += (sw.ElapsedTicks / (float)System.Diagnostics.Stopwatch.Frequency);
                                avgTickTime /= 2;
                                avgCreatedPerTick += nCreatedThisTick;
                                avgCreatedPerTick /= 2;
                                avgDestroyedPerTick += nDestroyedThisTick;
                                avgDestroyedPerTick /= 2;
                                ticksRemaining -= 1;

                                if(fullDump) {
                                        Debug.Log(DumpGameState());
                                }
                        }
                        if(ticksRemaining == 0) {
                                if(isHost && EveryoneIsReady()) {
                                        // Advance the game turn.
                                        var m = new NetworkMessage(NetworkMessage.Type.NextTurn);
                                        net.SendMessageToAll(m);

                                        currentGameState = DumpGameState();
                                        foreach(var p in players) {
                                                if(p.state != null) {
                                                        VerifyClientGameState(p.id, p.state);
                                                        p.state = null;
                                                }
                                        }

                                        ClearReady();
                                        serverNextTurn = true;
                                }

                                if(goForNextTurn) {
                                        if(isHost) {
                                                Log(currentGameState);
                                        }
                                        string state = null;
                                        if(syncCheckRequested || enableContinuousSyncCheck) {
                                                state = DumpGameState();
                                                Debug.Log(state);
                                                if(state.Length > 0xF000) { // Keep within max packet length.
                                                        state = null;
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
                                        ReadyUp(state);
                                        ticksRemaining = ticksPerTurn;
                                        turnID += 1;
                                }
                        }
                }

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

        void ReadyUp(string state) {
                if(replayInput != null) return;
                var m = new NetworkMessage(NetworkMessage.Type.Ready);
                m.gameState = state;
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
                foreach(Entity e in currentInstance.worldEntityCollisionCache) {
                        if(e.team == ignoreTeam) continue;

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
        public static Entity FindEntityWithinRadius(DVector2 origin, DReal radius, int ignoreTeam = -1, Func<Entity, DReal> getRadius = null) {
                getRadius = getRadius ?? (e => e.collisionRadius);
                foreach(Entity e in currentInstance.worldEntityCollisionCache) {
                        if(e.team == ignoreTeam) continue;

                        var r = getRadius(e);
                        if((e.position - origin).sqrMagnitude < radius * radius + r * r) {
                                return e;
                        }
                }

                return null;
        }

        // Locate an entity within the given circle, not on the given team.
        public static List<T> FindEntitiesWithinRadius<T>(DVector2 origin, DReal radius, int ignoreTeam = -1, Func<T, DReal> getRadius = null) where T : MonoBehaviour {
                var result = new List<T>();

                foreach(Entity e in currentInstance.worldEntityCollisionCache) {
                        T thing = (typeof(T) == typeof(Entity)) ? e as T : e.GetComponent<T>();
                        if(thing == null) continue;
                        if(e.team == ignoreTeam) continue;

                        var r = getRadius != null ? getRadius(thing) : e.collisionRadius;
                        if((e.position - origin).sqrMagnitude < radius * radius + r * r) {
                                result.Add(thing);
                        }
                }

                return result;
        }

        public static List<Entity> FindEntitiesWithinRadius(DVector2 origin, DReal radius, int ignoreTeam = -1, Func<Entity, DReal> getRadius = null) {
                return FindEntitiesWithinRadius<Entity>(origin, radius, ignoreTeam, getRadius);
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

                var m = new NetworkMessage(NetworkMessage.Type.Move);
                m.entityID = currentInstance.reverseWorldEntities[unit];
                m.position = position;
                currentInstance.net.SendMessageToServer(m);
        }

        public static void IssueAttack(Entity unit, Entity target) {
                if(currentInstance.replayInput != null) return;

                var m = new NetworkMessage(NetworkMessage.Type.Attack);
                m.entityID = currentInstance.reverseWorldEntities[unit];
                m.targetID = currentInstance.reverseWorldEntities[target];
                currentInstance.net.SendMessageToServer(m);
        }

        public static void IssueUIAction(Entity unit, int what) {
                if(currentInstance.replayInput != null) return;

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
                players.Sort((p1, p2) => p1.id - p2.id);
                if (p.id == localPlayerID) {
                        SetPlayerName(p, Lobby.localPlayerName);
                }
        }

        void PlayerLeave(int id) {
                if(id == localPlayerID) {
                        Debug.LogWarning("Saw myself leave?");
                }
                if(connectionState == ConnectionState.InGame) {
                        Debug.LogError("Player " + id + " " + PlayerFromID(id).name + " dropped.");
                }
                players.RemoveAll(p => p.id == id);
        }

        void PlayerUpdate(int id, string name, int team) {
                var p = PlayerFromID(id);
                if(name != null && name.Length != 0) {
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
                Log("Server sent message " + message + " " + message.type);
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
                        StartGame(message.levelName);
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

        private void StartGame(string levelName) {
                try {
                        if (!System.IO.Directory.Exists("Replays")) {
                                System.IO.Directory.CreateDirectory("Replays");
                        }
                        replayOutput = new System.IO.FileStream("Replays/" + System.DateTime.UtcNow.ToString("yyyy-MM-dd HH-mm-ss") + ".replay", System.IO.FileMode.CreateNew);
                }
                catch (System.Exception e) {
                        Debug.LogError("Unable to record replay.");
                        Debug.LogException(e, this);
                }
                Application.LoadLevel(levelName);
                teamResources = Enumerable.Range(0, 7).Select(_ => new ResourceSet { Metal = 2000 }).ToArray();
        }

        bool PlayerIsAdmin(int id) {
                return id == localPlayerID;
        }

        public void OnClientMessage(int playerID, NetworkMessage message) {
                Log("Player " + playerID + " sent message " + message + " " + message.type);
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
                        if(message.gameState != null) {
                                VerifyClientGameState(playerID, message.gameState);
                        }
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
                m.teamID = -1;
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

        public static void Trace(MonoBehaviour what, string msg) {
                if(currentInstance == null || !currentInstance.enableActionTracing) {
                        return;
                }
                var ent = what.GetComponent<Entity>();
                if(ent == null) {
                        Debug.Log("Non-entity " + what + ": " + msg);
                } else if(currentInstance.reverseWorldEntities.ContainsKey(ent)) {
                        Debug.Log("[" + currentInstance.reverseWorldEntities[ent] + "] " + what + ": " + msg);
                } else {
                        Debug.Log("Unknown entity " + what + ": " + msg);
                }
        }
}
