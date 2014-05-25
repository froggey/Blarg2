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
                entity = gameObject.GetComponent<Entity>();
                entity.AddUpdateAction(1, TickUpdate);
        }

        void TickUpdate() {
                if(target != null) {
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

                        Vector3 position = new Vector3((float)hitPosition.y, 0, (float)hitPosition.x);
                        Object.Instantiate(impactPrefab, position, Quaternion.AngleAxis((float)entity.rotation, Vector3.up));

                        if(trail) {
                                trail.transform.parent = null;
                                trail.autodestruct = true;
                                trail = null;
                        }

                        ComSat.DestroyEntity(entity);
                        return;
                }
        }
}
