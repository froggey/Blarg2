using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Projectile : MonoBehaviour {
        public int damage;
        public int initialSpeed;
        private DVector2 velocity;

        public GameObject impactPrefab;
        public TrailRenderer trail;

        Entity entity;

        void Awake() {
                entity = gameObject.GetComponent<Entity>();
        }

        void Start() {
                velocity = DVector2.FromAngle(entity.rotation) * initialSpeed;
        }

        void TickUpdate() {
                DVector2 newPosition = entity.position + velocity * ComSat.tickRate;

                DVector2 hitPosition;
                Entity hit = ComSat.LineCast(entity.position, newPosition, out hitPosition, entity.team);
                if(hit != null) {
                        print("Projectile at " + entity.position + " impacted " + hit + " at " + hitPosition);

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

                entity.position = newPosition;
        }
}
