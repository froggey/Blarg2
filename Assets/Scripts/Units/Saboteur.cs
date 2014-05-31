using UnityEngine;
using System.Collections;

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
                moving = false;
                motor = GetComponent<Vehicle>();
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
        }

        void TickUpdate() {
                if(target != null) {
                        destination = target.position;

                        if((target.position - entity.position).sqrMagnitude < target.collisionRadius * target.collisionRadius) {
                                target.GetComponent<Factory>().Sabotage();
                                ComSat.DestroyEntity(entity);
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
                moving = true;
                destination = location;
        }

        void Attack(Entity target) {
                if(target == entity) {
                        return;
                }
                var factory = target.GetComponent<Factory>();
                if(factory == null) {
                        return;
                }

                this.target = target;
                moving = true;
        }
}
