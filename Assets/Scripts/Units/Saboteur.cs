using UnityEngine;
using System.Collections;
using System.Linq;

public interface ISabotagable {
        void Sabotage();
}

[RequireComponent (typeof(Vehicle))]
[RequireComponent (typeof(Entity))]
public class Saboteur : MonoBehaviour {
        bool moving;
        DVector2 destination;
        Entity target;

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

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(ComSat.EntityExists(target)) {
                        destination = target.position;

                        var radius = target.collisionRadius + entity.collisionRadius;
                        if((target.position - entity.position).sqrMagnitude < radius * radius) {
                                var components = target.GetComponents(typeof(ISabotagable));

                                foreach(var c in components) {
                                        (c as ISabotagable).Sabotage();
                                }

                                ComSat.DestroyEntity(entity, DestroyReason.HitTarget);
                        }
                }
                if(moving) {
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
                target = null;
        }

        void Attack(Entity[] targets) {
                ComSat.Trace(this, "Attack");
                var validTargets = targets.Where(t => ComSat.EntityExists(t) && t.GetComponent(typeof(ISabotagable)) != null).OrderBy(t => (t.position - entity.position).sqrMagnitude);
                if (!validTargets.Any()) return;
                target = validTargets.First();
                moving = true;
        }
}
