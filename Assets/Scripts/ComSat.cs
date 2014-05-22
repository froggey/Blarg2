using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ComSat : MonoBehaviour {
        private class Player {
                public int id;
                public string name;
                public int team;
                public bool ready;
                public int unreadyTime;

                public NetworkPlayer client;
        }

        // Game runs at this rate.
        public static DReal tickRate = (DReal)1 / (DReal)25;
        // This many ticks per communication turn.
        // Max input lag = ticksPerTurn * tickRate.
        public static int ticksPerTurn = 5;

        private float timeSlop;
        private int ticksRemaining; // Ticks remaining in this turn.
        private bool goForNextTurn;

        private int nextEntityId;
        private Dictionary<int, Entity> worldEntities;
        private Dictionary<Entity, int> reverseWorldEntities;
        private List<Projectile> worldProjectiles;
        private List<Entity> worldEntityCache;

        // Actions to be performed at the end of a tick.
        private List<System.Action> deferredActions;
        private List<System.Action> queuedCommands;

        private static ComSat currentInstance;

        private List<Player> players;

        private bool worldRunning;

        void OnGUI() {
                if(!worldRunning) return;

                GUILayout.BeginArea(new Rect (Screen.width-200, 0, 200, 100));
                if(GUILayout.Button("Disconnect")) {
                        Network.Disconnect();
                        Destroy(gameObject);
                        Application.LoadLevel("Lobby");
                }
                GUILayout.EndArea();
        }

        void OnDisconnectedFromServer() {
                Destroy(gameObject);
                Application.LoadLevel("Lobby");
        }

        void OnDestroy() {
                currentInstance = null;
        }

        void Awake() {
                if(currentInstance != null) {
                        throw new System.Exception("Multiple ComSat instances!");
                }

                DontDestroyOnLoad(gameObject);

                currentInstance = this;

                if(Network.isServer) {
                        players = new List<Player>();
                }
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

        // Create a new entity at whereever.
        // Called by all, but ignored everywhere but the server.
        public static void Spawn(string entityName, int team, DVector2 position, DReal rotation) {
                if(!Network.isServer) return;

                currentInstance.networkView.RPC("SpawnCommand",
                                                RPCMode.AllBuffered,
                                                entityName,
                                                team,
                                                DReal.Serialize(position.x), DReal.Serialize(position.y),
                                                DReal.Serialize(rotation));
        }

        // (Client)
        [RPC]
        void SpawnCommand(string entityName, int team, string positionX, string positionY, string rotation_) {
                var position = new DVector2(DReal.Deserialize(positionX), DReal.Deserialize(positionY));
                var rotation = DReal.Deserialize(rotation_);
                GameObject go = Resources.Load<GameObject>(entityName);
                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);

                QueueCommand(() => {
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
        }

        public static void DestroyEntity(Entity e) {
                currentInstance.deferredActions.Add(() => {
                                if(!currentInstance.reverseWorldEntities.ContainsKey(e)) {
                                        // It's possible for a thing to be destroyed twice in one tick.
                                        return;
                                }
                                int id = currentInstance.reverseWorldEntities[e];
                                currentInstance.worldEntities.Remove(id);
                                currentInstance.reverseWorldEntities.Remove(e);
                                currentInstance.worldEntityCache.Remove(e);
                                Object.Destroy(e.gameObject);
                        });
        }

        // (Client)
        [RPC]
        void MoveCommand(int entityID, string positionX, string positionY) {
                var position = new DVector2(DReal.Deserialize(positionX), DReal.Deserialize(positionY));
                var entity = worldEntities[entityID];
                QueueCommand(() => {
                                if(entity != null) {
                                        entity.gameObject.SendMessage("Move", position, SendMessageOptions.DontRequireReceiver);
                                }
                        });
        }

        // (Client)
        [RPC]
        void AttackCommand(int entityID, int targetID) {
                var entity = worldEntities[entityID];
                var target = worldEntities[targetID];
                QueueCommand(() => {
                                if(entity != null && target != null) {
                                        entity.gameObject.SendMessage("Attack", target, SendMessageOptions.DontRequireReceiver);
                                }
                        });
        }

        // (Client)
        [RPC]
        void UIActionCommand(int entityID, int what) {
                var entity = worldEntities[entityID];
                QueueCommand(() => {
                                if(entity != null) {
                                        entity.gameObject.SendMessage("UIAction", what, SendMessageOptions.DontRequireReceiver);
                                }
                        });
        }

        // (Client)
        void QueueCommand(System.Action command) {
                queuedCommands.Add(command);
        }

        private static uint randomValue;

        void OnLevelWasLoaded(int level) {
                worldRunning = true;

                timeSlop = 0.0f;
                ticksRemaining = 0;
                nextEntityId = 1;
                randomValue = 42;
                goForNextTurn = false;

                worldEntities = new Dictionary<int, Entity>();
                reverseWorldEntities = new Dictionary<Entity, int>();
                worldProjectiles = new List<Projectile>();
                worldEntityCache = new List<Entity>(); // Faster to iterate through.
                deferredActions = new List<System.Action>();
                queuedCommands = new List<System.Action>();

                if(Network.isServer) {
                        ClearReady();
                }

                ReadyUp();
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
                                        Debug.LogWarning("Player " + p.name + "[" + p.id + "] is lagging.");
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
                                e.gameObject.SendMessage("TickUpdate", null, SendMessageOptions.DontRequireReceiver);
                        }
                }

                // Lists are ordered, so are consistent anyway.
                foreach(Projectile e in worldProjectiles) {
                        if(e != null) {
                                e.gameObject.SendMessage("TickUpdate", null, SendMessageOptions.DontRequireReceiver);
                        }
                }

                foreach(var a in deferredActions) {
                        a();
                }
                deferredActions.Clear();
        }

        void Update() {
                if(!worldRunning) return;

                // FixedUpdate has an indeterminate update order, TickUpdate fixes this.
                // First the world is updated, then entities are updated in creation (id) order.
                timeSlop += Time.deltaTime;
                while(timeSlop >= (float)tickRate) {
                        timeSlop -= (float)tickRate;
                        if(ticksRemaining != 0) {
                                TickUpdate();
                                ticksRemaining -= 1;
                        }
                        if(ticksRemaining == 0 && goForNextTurn) {
                                foreach(System.Action a in queuedCommands) {
                                        a();
                                }
                                queuedCommands.Clear();
                                goForNextTurn = false;
                                ReadyUp();
                                ticksRemaining = ticksPerTurn;
                        }

                        if(Network.isServer && ticksRemaining == 0 && EveryoneIsReady()) {
                                // Advance the game turn.
                                networkView.RPC("NextTurn", RPCMode.AllBuffered);
                                ClearReady();
                        }
                }
        }

        void ReadyUp() {
                if(Network.isServer) {
                        foreach(var p in players) {
                                if(p.id == 0) {
                                        p.ready = true;
                                        return;
                                }
                        }
                } else {
                        networkView.RPC("DoReadyUp", RPCMode.Server);
                }
        }

        // Mark a client as ready. (Server)
        [RPC]
        void DoReadyUp(NetworkMessageInfo info) {
                foreach(var p in players) {
                        if(p.client == info.sender) {
                                //print("Player " + p.id + " readyup");
                                p.ready = true;
                                return;
                        }
                }
                Debug.LogWarning("ReadyUp from unknown client " + info);
        }

        // Advance to the next communication turn. (Client)
        [RPC]
        void NextTurn() {
                if(goForNextTurn) {
                        Debug.LogError("Duplicate NextTurn call!");
                }
                goForNextTurn = true;
        }

        public static void SpawnProjectile(Entity origin, GameObject prefab, DVector2 position, DReal rotation/*, DReal height*/) {
                currentInstance.deferredActions.Add(() => {
                                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);
                                var thing = (Object.Instantiate(prefab, worldPosition, worldRotation) as GameObject).GetComponent<Projectile>();
                                thing.position = position;
                                thing.rotation = rotation;
                                if(origin != null) {
                                        thing.team = origin.team;
                                        thing.origin = origin;
                                }
                                currentInstance.worldProjectiles.Add(thing);
                        });
        }

        public static void DestroyProjectile(Projectile p) {
                currentInstance.deferredActions.Add(() => {
                                currentInstance.worldProjectiles.Remove(p);
                                Object.Destroy(p.gameObject);
                        });
        }

        Player SenderToPlayer(NetworkPlayer net) {
                foreach(var p in players) {
                        if(p.client == net) {
                                return p;
                        }
                }
                return null;
        }

        public static void IssueMove(Entity unit, DVector2 position) {
                //Debug.Log("Move " + unit + "[" + reverseWorldEntities[unit] + "] to " + position);
                if(Network.isServer) {
                        currentInstance.IssueMoveServer(currentInstance.players[0], unit, position);
                } else {
                        currentInstance.networkView.RPC("IssueMoveNet", RPCMode.Server,
                                                        currentInstance.reverseWorldEntities[unit],
                                                        DReal.Serialize(position.x), DReal.Serialize(position.y));
                }
        }

        void IssueMoveServer(Player player, Entity unit, DVector2 position) {
                if(unit == null || player.team != unit.team) {
                        return;
                }

                networkView.RPC("MoveCommand", RPCMode.AllBuffered,
                                reverseWorldEntities[unit],
                                DReal.Serialize(position.x), DReal.Serialize(position.y));
        }

        [RPC]
        void IssueMoveNet(int entityID, string positionX, string positionY, NetworkMessageInfo info) {
                Entity e = worldEntities[entityID];
                if(e == null) {
                        return;
                }
                var position = new DVector2(DReal.Deserialize(positionX), DReal.Deserialize(positionY));
                IssueMoveServer(SenderToPlayer(info.sender), e, position);
        }

        public static void IssueAttack(Entity unit, Entity target) {
                //Debug.Log(unit + "[" + reverseWorldEntities[unit] + "] attack " + target + "[" + reverseWorldEntities[target] + "]");
                if(Network.isServer) {
                        currentInstance.IssueAttackServer(currentInstance.players[0], unit, target);
                } else {
                        currentInstance.networkView.RPC("IssueAttackNet", RPCMode.Server,
                                                        currentInstance.reverseWorldEntities[unit],
                                                        currentInstance.reverseWorldEntities[target]);
                }
        }

        void IssueAttackServer(Player player, Entity unit, Entity target) {
                if(unit == null || target == null || player.team != unit.team) {
                        return;
                }

                networkView.RPC("AttackCommand", RPCMode.AllBuffered,
                                reverseWorldEntities[unit],
                                reverseWorldEntities[target]);
        }

        [RPC]
        void IssueAttackNet(int entityID, int targetID, NetworkMessageInfo info) {
                Entity e = worldEntities[entityID];
                Entity t = worldEntities[targetID];
                if(e == null || t == null) {
                        return;
                }
                IssueAttackServer(SenderToPlayer(info.sender), e, t);
        }

        public static void IssueUIAction(Entity unit, int what) {
                //Debug.Log(unit + "[" + reverseWorldEntities[unit] + "] UI action " + what);
                if(Network.isServer) {
                        currentInstance.IssueUIActionServer(currentInstance.players[0], unit, what);
                } else {
                        currentInstance.networkView.RPC("IssueUIActionNet", RPCMode.Server,
                                                        currentInstance.reverseWorldEntities[unit],
                                                        what);
                }
        }

        void IssueUIActionServer(Player player, Entity unit, int what) {
                if(unit == null || player.team != unit.team) {
                        return;
                }

                networkView.RPC("UIActionCommand", RPCMode.AllBuffered,
                                reverseWorldEntities[unit],
                                what);
        }

        [RPC]
        void IssueUIActionNet(int entityID, int what, NetworkMessageInfo info) {
                Entity e = worldEntities[entityID];
                if(e == null) {
                        return;
                }
                IssueUIActionServer(SenderToPlayer(info.sender), e, what);
        }

        // Cast a line from A to B, checking for collisions with other entities.
        public static Entity LineCast(DVector2 start, DVector2 end, out DVector2 hitPosition, int ignoreTeam = -1) {
                foreach(Entity e in currentInstance.worldEntityCache) {
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
        public static Entity FindEntityWithinRadius(DVector2 origin, DReal radius, int ignoreTeam = -1) {
                foreach(Entity e in currentInstance.worldEntityCache) {
                        if(e.team == ignoreTeam) continue;

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

        public void ServerAddPlayer(int id, NetworkPlayer client, string name, int team) {
                var p = new Player();
                p.id = id;
                p.client = client;
                p.name = name;
                p.team = team;
                players.Add(p);
        }
}
