using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Vehicle))]
[RequireComponent (typeof(Entity))]
public class LandRover : MonoBehaviour {
        bool moving;
        DVector2 destination;
        Entity target;

        public GameObject impactPrefab;
        public int detonateRange;
        public int explosionRadius;
        public int damageAtCentre;

        // This close is close enough.
        static DReal sqrPositioningAccuracy = (DReal)1 / 100;

        Vehicle motor;
        Entity entity;

        void Awake() {
                ComSat.Trace(this, "Awake");
                moving = false;
                motor = GetComponent<Vehicle>();
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
        }

        void Detonate() {
                ObjectPool.Instantiate(impactPrefab, transform.position, transform.rotation);
                var sqrRadius = explosionRadius * explosionRadius;
                foreach(var e in ComSat.FindEntitiesWithinRadius(entity.position, explosionRadius)) {
                        var sqrDist = (e.position - entity.position).sqrMagnitude;
                        var power = (sqrRadius - sqrDist) / sqrRadius;
                        print("Exploding on " + e + "  sd: " + sqrDist + "  sr: " + sqrRadius + "  pwr: " + power + "  dam: " + (int)(damageAtCentre * power));
                        e.Damage((int)(damageAtCentre * power));
                }
                ComSat.DestroyEntity(entity);
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(ComSat.EntityExists(target)) {
                        moving = true;
                        destination = target.position;
                        if((destination - entity.position).sqrMagnitude < detonateRange * detonateRange) {
                                Detonate();
                                return;
                        }
                } else {
                        target = null;
                }
                if(moving) {
                        if((ComSat.RandomValue() % 500) == 0) {
                                Detonate();
                                return;
                        }
                        if((destination - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                                // Close enough.
                                moving = false;
                                motor.Stop();
                        } else {
                                motor.MoveTowards(destination);
                        }
                }
        }

        void Move(DVector2 location) {
                ComSat.Trace(this, "Move");
                moving = true;
                destination = location;
        }

        void Attack(Entity target) {
                this.target = target;
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, detonateRange);

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
}
