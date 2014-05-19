using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class TwinTank : MonoBehaviour {
        // Can't use GameObjects to set these up because floats.
        // Unity refuses to edit these because they're structs.
        // :(
        public DReal projectileSpawnDistance = 6;
        public DVector2 turretAttachPoint = new DVector2(0, -(DReal)597 / 2048);
        public DReal turretSeperation = (DReal)1 / 3;
        public GameObject turretMesh;

        private Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
        }

        enum Mode {
                IDLE, MOVE, ATTACK
        }

        static DReal attackDistance = 300; // Try to stay this close.
        static DReal speed = 93; // m/s
        static DReal turnSpeed = DReal.Radians(687); // radians per second
        static DReal turretTurnSpeed = DReal.Radians(727); // radians per second
        static DReal sqrPositioningAccuracy = (DReal)1 / 100;
        static DReal maxMoveAngle = DReal.Radians(100);

        Mode mode;
        DVector2 destination;
        Entity target;

        DReal turretAngle;

        void Start() {
                mode = Mode.IDLE;
                turretAngle = 0;
        }

        // current = current angle, radians.
        // target = target angle, radians.
        // speed = max radians turned per second.
        // Returns the new angle in radians, range [0,2pi].
        DReal CalculateNewAngle(DReal currentAngle, DReal targetAngle, DReal speed) {
                var turnSpeedTicks = speed * ComSat.tickRate;
                targetAngle = DReal.Mod(targetAngle, DReal.TwoPI);

		// Turn towards heading.
		var angleDiff = DReal.Mod(currentAngle - targetAngle, DReal.TwoPI);
                int sign;
                DReal distance;
                if(angleDiff > DReal.PI) {
                        sign = 1;
                        distance = DReal.TwoPI - angleDiff;
                } else {
                        sign = -1;
                        distance = angleDiff;
                }
                if(distance > turnSpeedTicks) {
                        currentAngle += turnSpeedTicks * sign;
                } else {
                        currentAngle = targetAngle;
                }

                return DReal.Mod(currentAngle, DReal.TwoPI);
        }

        void MoveTowards(DVector2 dest) {
                var dir = dest - entity.position; // also vector to dest.
                var targetAngle = DReal.Atan2(dir.y, dir.x);
                var baseAngle = CalculateNewAngle(entity.rotation, targetAngle, turnSpeed);
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
                        var travel = new DVector2(DReal.Cos(baseAngle), DReal.Sin(baseAngle)) * (1 - (diff / DReal.PI)) * tickSpeed;
                        entity.position += travel;
		}
	}

        void TurnTurret(DReal targetAngle) {
                turretAngle = CalculateNewAngle(turretAngle, targetAngle, turretTurnSpeed);
        }

        void TickUpdate() {
                if(mode == Mode.ATTACK && target == null) {
                        target = null;
                        mode = Mode.IDLE;
                }

                if(mode == Mode.ATTACK) {
                        DVector2 dir = target.position - entity.position;
                        DReal turretAngle = DReal.Atan2(dir.y, dir.x);
                        print("attack angle is " + turretAngle + "[" + (turretAngle - entity.rotation) + "][" + DReal.Mod(turretAngle - entity.rotation, DReal.TwoPI) + "] for dir " + dir);
                        TurnTurret(turretAngle - entity.rotation);
                        print("New angle is " + this.turretAngle);

                        if((target.position - entity.position).sqrMagnitude < attackDistance * attackDistance) {
                                // FIRE! (maybe)
                        } else {
                                // Move towards.
                                MoveTowards(target.position);
                        }
                } else if(mode == Mode.MOVE) {
                        // Move towards.
                        if((destination - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                                // Close enough.
                                mode = Mode.IDLE;
                        } else {
                                MoveTowards(destination);
                        }
                        TurnTurret(0);
                } else if(mode == Mode.IDLE) {
                        TurnTurret(0);
                }
        }

        void Attack(Entity target) {
                if(target == entity) {
                        return;
                }
                Debug.Log(this + " attacking " + target);
                mode = Mode.ATTACK;
                this.target = target;
        }

        void Move(DVector2 location) {
                Debug.Log(this + " moving to " + location);
                mode = Mode.MOVE;
                target = null;
                destination = location;
        }

        void Update() {
                // Always update local rotation.
                // Touching the world rotation causes fighting with Entity.
                turretMesh.transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(turretAngle), Vector3.up);
        }

        void OnDrawGizmosSelected() {
                // Projectile spawn location & stuff.
                Gizmos.color = Color.red;
                Vector3 turretPosition = new Vector3((float)turretAttachPoint.x,
                                                     0,
                                                     (float)turretAttachPoint.y);
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale) *
                        Matrix4x4.TRS(turretPosition, Quaternion.AngleAxis((float)DReal.Degrees(turretAngle), Vector3.up), transform.lossyScale);
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawWireSphere(new Vector3(0,0,0), 0.5f);
                Gizmos.DrawWireCube(new Vector3((float)turretSeperation, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));
                Gizmos.DrawWireCube(new Vector3(-(float)turretSeperation, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));
        }

}
