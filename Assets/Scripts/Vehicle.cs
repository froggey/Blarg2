using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Vehicle : MonoBehaviour {
        public int maxSpeed; // m/s
        public int turnSpeed; // degrees/s

        static DReal maxMoveAngle = DReal.Radians(100);

        private Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
        }

        // This could be smarter. If dest is too close & perpendicular, then the tank
        // can end up circling around.
        public void MoveTowards(DVector2 dest) {
                var dir = dest - entity.position; // also vector to dest.
                var targetAngle = DVector2.ToAngle(dir);
                var baseAngle = Utility.CalculateNewAngle(entity.rotation, targetAngle, DReal.Radians(turnSpeed));
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
                        var tickSpeed = maxSpeed * ComSat.tickRate;
                        var distance = dir.magnitude;
                        print("Distance: " + distance + "  speed is: " + tickSpeed);
                        if(distance < tickSpeed) {
                                tickSpeed = distance;
                        }
                        var travel = DVector2.FromAngle(baseAngle) * (1 - (diff / DReal.PI)) * tickSpeed;
                        entity.position += travel;
		}
	}
}
