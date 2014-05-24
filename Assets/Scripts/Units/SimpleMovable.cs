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
                moving = false;
                motor = GetComponent<Vehicle>();
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(3, TickUpdate);
        }

        void TickUpdate() {
                if(moving) {
                        if((destination - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                                // Close enough.
                                moving = false;
                        } else {
                                motor.MoveTowards(destination);
                        }
                }
        }

        void Move(DVector2 location) {
                Debug.Log(this + " moving to " + location);
                moving = true;
                destination = location;
        }
}
