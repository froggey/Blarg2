using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Projectile : MonoBehaviour {
        public int damage;
        public int initialSpeed;
        public int turnSpeed;
        public Entity target;

        public GameObject impactPrefab;
        public TrailRenderer trail;

        Entity entity;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = gameObject.GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                entity.AddInstantiateAction(OnInstantiate);
        }

        void OnInstantiate() {
                target = null;
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(ComSat.EntityExists(target)) {
                        var dir = target.position - entity.position; // also vector to dest.
                        var targetAngle = DVector2.ToAngle(dir);
                        var baseAngle = Utility.CalculateNewAngle(entity.rotation, targetAngle, DReal.Radians(turnSpeed));
                        entity.rotation = baseAngle;
                }
                entity.velocity = DVector2.FromAngle(entity.rotation) * initialSpeed;
                DVector2 newPosition = entity.position + entity.velocity * ComSat.tickRate;

                // FIXME: this should do something to account for hitting fast-moving projectiles.
                DVector2 hitPosition;
                Entity hit = ComSat.LineCast(entity.position, newPosition, out hitPosition, entity.team);
                if(hit != null && (!hit.hitOnlyIfTargetted || hit == target)) {
                        hit.Damage(damage);

                        var position = new Vector3((float)hitPosition.y, 0, (float)hitPosition.x);
                        var rotation = Quaternion.AngleAxis((float)entity.rotation, Vector3.up);
                        if(impactPrefab != null && ComSat.RateLimit()) {
                                ObjectPool.Instantiate(impactPrefab, position, rotation);
                        }

                        //if(trail) {
                        //        trail.transform.parent = null;
                        //        trail.autodestruct = true;
                        //        trail = null;
                        //}

                        ComSat.DestroyEntity(entity, DestroyReason.HitTarget);
                        return;
                }
        }
}
