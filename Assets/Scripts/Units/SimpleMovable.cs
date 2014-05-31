using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Vehicle))]
[RequireComponent (typeof(Entity))]
public class SimpleMovable : MonoBehaviour {
        bool moving;
        DVector2 destination;

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
        }
}
