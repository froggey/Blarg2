using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

static class ComSat {
        public static DReal tickRate = (DReal)1 / (DReal)25;
        private static float timeSlop;
        private static int nextEntityId;
        private static Dictionary<int, Entity> worldEntities = new Dictionary<int, Entity>();
        private static Dictionary<Entity, int> reverseWorldEntities = new Dictionary<Entity, int>();
        private static List<Projectile> worldProjectiles = new List<Projectile>();
        private static List<Entity> worldEntityCache = new List<Entity>(); // Faster to iterate through.

        private static WorldSimulation world_;
        public static WorldSimulation world {
                get { return world_; }
        }

        // Create a new entity at whereever.
        public static Entity Spawn(string entityName, int team, DVector2 position, DReal rotation) {
                GameObject go = Resources.Load<GameObject>(entityName);
                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);
                Entity thing = (Object.Instantiate(go, worldPosition, worldRotation) as GameObject).GetComponent<Entity>();
                thing.position = position;
                thing.rotation = rotation;
                thing.team = team;
                return thing;
        }

        // Called by WorldSimulation when the level is loaded.
        public static void LevelLoad(WorldSimulation world) {
                if(worldEntities.Count != 0) {
                        // This test is done here, not in LevelUnload to prevent spurious
                        // invalid entity destroyed warnings.
                        Debug.LogError("Not all entities destroyed before starting new level!");
                        worldEntities.Clear();
                        reverseWorldEntities.Clear();
                }
                timeSlop = 0.0f;
                nextEntityId = 0;
                ComSat.world_ = world;
        }

        // Called by WorldSimulation when the level is unloaded.
        public static void LevelUnload() {
                ComSat.world_ = null;
                worldProjectiles.Clear();
        }

        // Called every tickRate seconds when the world is live.
        private static void TickUpdate() {
                world.TickUpdate();
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
        }

        public static void Update() {
                // FixedUpdate has an indeterminate update order, TickUpdate fixes this.
                // First the world is updated, then entities are updated in creation (id) order.
                timeSlop += Time.deltaTime;
                while(world && timeSlop >= (float)tickRate) {
                        timeSlop -= (float)tickRate;
                        TickUpdate();
                }
        }

        public static void EntityCreated(Entity ent) {
                int id = ++nextEntityId;
                worldEntities[id] = ent;
                reverseWorldEntities[ent] = id;
                worldEntityCache.Add(ent);
        }

        public static void EntityDestroyed(Entity ent) {
                if(!reverseWorldEntities.ContainsKey(ent)) {
                        throw new System.Exception("Invalid entity " + ent + " being destroyed!");
                }
                int id = reverseWorldEntities[ent];
                worldEntities.Remove(id);
                reverseWorldEntities.Remove(ent);
                worldEntityCache.Remove(ent);
        }

        public static Projectile SpawnProjectile(Entity origin, GameObject prefab, DVector2 position, DReal rotation/*, DReal height*/) {
                Vector3 worldPosition = new Vector3((float)position.y, 0, (float)position.x);
                Quaternion worldRotation = Quaternion.AngleAxis((float)rotation, Vector3.up);
                Projectile thing = (Object.Instantiate(prefab, worldPosition, worldRotation) as GameObject).GetComponent<Projectile>();
                thing.position = position;
                thing.rotation = rotation;
                //thing.height = height;
                if(origin != null) {
                        thing.team = origin.team;
                        thing.origin = origin;
                }
                return thing;
        }

        public static void ProjectileCreated(Projectile projectile) {
                worldProjectiles.Add(projectile);
        }
        public static void ProjectileDestroyed(Projectile projectile) {
                worldProjectiles.Remove(projectile);
        }

        public static void IssueMove(Entity unit, DVector2 location) {
                Debug.Log("Move " + unit + "[" + reverseWorldEntities[unit] + "] to " + location);
                unit.gameObject.SendMessage("Move", location, SendMessageOptions.DontRequireReceiver);
        }

        public static void IssueAttack(Entity unit, Entity target) {
                Debug.Log(unit + "[" + reverseWorldEntities[unit] + "] attack " + target + "[" + reverseWorldEntities[target] + "]");
                unit.gameObject.SendMessage("Attack", target, SendMessageOptions.DontRequireReceiver);
        }

        // Cast a line from A to B, checking for collisions with other entities.
        public static Entity LineCast(DVector2 start, DVector2 end, out DVector2 hitPosition, int ignoreTeam = -1) {
                foreach(Entity e in worldEntityCache) {
                        if(e.team == ignoreTeam) continue;

                        DVector2 result;
                        if(Utility.IntersectLineCircle(e.position, e.collisionRadius, start, end, out result)) {
                                hitPosition = result;
                                return e;
                        }
                }
                hitPosition = new DVector2();

                return null;
        }
}
