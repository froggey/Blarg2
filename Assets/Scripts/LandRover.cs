using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class LandRover : MonoBehaviour {
        bool moving;
        DVector2 destination;

        static DReal speed = 10; // m/s
        static DReal turnSpeed = DReal.Radians(90); // radians per second
        static DReal sqrPositioningAccuracy = (DReal)1 / 100;
        static DReal maxMoveAngle = DReal.Radians(100);

        public DReal collisionRadius = (DReal)3 / 2; // 2.5 Ughhh!

        private Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.collisionRadius = collisionRadius; // ugghhh.
                moving = false;
        }

        // This could be smarter. If dest is too close & perpendicular, then the tank
        // can end up circling around.
        void MoveTowards(DVector2 dest) {
                var dir = dest - entity.position; // also vector to dest.
                var targetAngle = DVector2.ToAngle(dir);
                var baseAngle = Utility.CalculateNewAngle(entity.rotation, targetAngle, turnSpeed);
                entity.rotation = baseAngle;

		// Move along current heading. Ramp speed up as the angle gets closer.
                // Augh.
                // [-pi,pi] => [0,2pi]
                if(targetAngle < 0) {
                        targetAngle += DReal.TwoPI;
                }
                // Get targetAngle within +/- pi of baseAngle.
                if(targetAngle < baseAngle - DReal.PI) {
                        targetAngle += DReal.TwoPI;
                } else if(targetAngle > baseAngle + DReal.PI) {
                        targetAngle -= DReal.TwoPI;
                }
                var diff = DReal.Abs(baseAngle - targetAngle);
		if(diff < maxMoveAngle) {
                        var tickSpeed = speed * ComSat.tickRate;
                        var distance = dir.magnitude;
                        print("Distance: " + distance + "  speed is: " + tickSpeed);
                        if(distance < tickSpeed) {
                                tickSpeed = distance;
                        }
                        var travel = DVector2.FromAngle(baseAngle) * (1 - (diff / DReal.PI)) * tickSpeed;
                        entity.position += travel;
		}
	}

        void TickUpdate() {
                if(moving) {
                        if((destination - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                                // Close enough.
                                moving = false;
                        } else {
                                MoveTowards(destination);
                        }
                }
        }

        void Move(DVector2 location) {
                Debug.Log(this + " moving to " + location);
                moving = true;
                destination = location;
        }

        void OnDrawGizmosSelected() {
                // Ughhh!!
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, (float)collisionRadius);
        }
}
