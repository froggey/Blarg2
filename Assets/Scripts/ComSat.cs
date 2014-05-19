using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

static class ComSat {
        public static DReal tickRate = (DReal)1 / (DReal)25;
        private static float timeSlop;
        private static Dictionary<int, Entity> worldEntities = new Dictionary<int, Entity>();
        private static Dictionary<Entity, int> reverseWorldEntities = new Dictionary<Entity, int>();
        private static int nextEntityId;

        private static WorldSimulation world_;
        public static WorldSimulation world {
                get { return world_; }
        }

        // Create a new entity at whereever.
        public static Entity Spawn(string entityName, int team, DVector2 position, DReal rotation) {
                GameObject go = Resources.Load<GameObject>(entityName);
                Entity thing = (Object.Instantiate(go) as GameObject).GetComponent<Entity>();
                thing.position = position;
                thing.rotation = rotation;
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
        }

        // Called every tickRate seconds when the world is live.
        private static void TickUpdate() {
                world.TickUpdate();
                var items = from pair in worldEntities
                        orderby pair.Key ascending
                        select pair.Value;

                foreach(Entity e in items) {
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
        }

        public static void EntityDestroyed(Entity ent) {
                if(!reverseWorldEntities.ContainsKey(ent)) {
                        throw new System.Exception("Invalid entity " + ent + " being destroyed!");
                }
                int id = reverseWorldEntities[ent];
                worldEntities.Remove(id);
                reverseWorldEntities.Remove(ent);
        }

        public static void IssueMove(Entity unit, DVector2 location) {
                Debug.Log("Move " + unit + "[" + reverseWorldEntities[unit] + "] to " + location);
                unit.gameObject.SendMessage("Move", location, SendMessageOptions.DontRequireReceiver);
        }

        public static void IssueAttack(Entity unit, Entity target) {
                Debug.Log(unit + "[" + reverseWorldEntities[unit] + "] attack " + target + "[" + reverseWorldEntities[target] + "]");
                unit.gameObject.SendMessage("Attack", target, SendMessageOptions.DontRequireReceiver);
        }
}
