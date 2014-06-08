using UnityEngine;

// When colliding with another entity on the same plane, forcefully move away.
[RequireComponent (typeof(Entity))]
public class CollisionResponse : MonoBehaviour {
        private Entity entity;

        public enum Layer {
                GROUND, AIR
        }

        public Layer layer;
        public bool fixedPosition;
        public bool canPush = true;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
        }

        void TickUpdate() {
                foreach(var ent in ComSat.FindEntitiesWithinRadius(entity.position, entity.collisionRadius)) {
                        if(ent == entity) continue;
                        var cr = ent.GetComponent<CollisionResponse>();
                        if(cr != null && cr.layer == this.layer && !cr.fixedPosition && (canPush || !cr.canPush)) {
                                var maxDist = entity.collisionRadius + ent.collisionRadius;
                                var sqrMaxDist = maxDist * maxDist;
                                var sqrDist = (ent.position - entity.position).sqrMagnitude;
                                var puntPower = sqrMaxDist / (sqrMaxDist - sqrDist) / 2;
                                var dir = (ent.position - entity.position).normalized;
                                ent.position += dir * puntPower;
                        }
                }
        }
}