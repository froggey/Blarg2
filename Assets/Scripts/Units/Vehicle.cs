using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Vehicle : MonoBehaviour {
        public int minSpeed; // m/s
        public int maxSpeed; // m/s
        public int turnSpeed; // degrees/s
        public bool canMoveWithoutTurning;

        static DReal maxMoveAngle = DReal.Radians(100);

        // How far one loop of the animation moves the vehicle.
        public float animationDistance = 1.0f;
        public Animator animator;

        private Entity entity;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
        }

        // This could be smarter. If dest is too close & perpendicular, then the tank
        // can end up circling around.
        public void MoveTowards(DVector2 dest) {
                ComSat.Trace(this, "MoveTowards");
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
		if(canMoveWithoutTurning || diff < maxMoveAngle) {
                        var distance = dir.magnitude;
                        //print("Distance: " + distance + "  speed is: " + tickSpeed);
                        var speed = minSpeed + (maxSpeed - minSpeed) * (1 - (diff / DReal.PI));
                        if(distance < speed) {
                                speed = DReal.Max(minSpeed, distance);
                        }
                        entity.velocity = canMoveWithoutTurning ? dir.normalized * speed : DVector2.FromAngle(baseAngle) * speed;
		} else {
                        Stop();
                }
	}

        public void Stop() {
                ComSat.Trace(this, "Stop");
                entity.velocity = DVector2.FromAngle(entity.rotation) * minSpeed;
        }

        void Update() {
                if(animator) {
                        float speed = (float)entity.velocity.magnitude;
                        animator.SetFloat("Speed", speed);
                        animator.speed = speed / animationDistance;
                        if(speed < 0.1) {
                                animator.speed = 1;
                        }
                }
        }
}
