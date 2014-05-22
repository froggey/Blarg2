using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(Vehicle))]
public class TwinTank : MonoBehaviour {
        // Can't use GameObjects to set these up because floats.
        // Unity refuses to edit these because they're structs.
        // :(
        public DReal projectileSpawnDistance = 6;
        public DVector2 turretAttachPoint = new DVector2(0, -(DReal)597 / 2048);
        public DReal turretSeperation = (DReal)1 / 3;
        public GameObject turretMesh;
        public GameObject projectilePrefab;
        public GameObject leftBarrel, rightBarrel;

        private Entity entity;
        private Vehicle vehicle;

        void Awake() {
                entity = GetComponent<Entity>();
                vehicle = GetComponent<Vehicle>();

                mode = Mode.IDLE;
                turretRotation = 0;

                fireCycle = FireCycle.READY;
        }

        enum Mode {
                IDLE, MOVE, ATTACK
        }

        static DReal attackDistance = 50; // Try to stay this close.
        static DReal attackRange = 60; // Maximum firing range.
        static DReal turretTurnSpeed = DReal.Radians(727); // radians per second
        static DReal sqrPositioningAccuracy = (DReal)1 / 100;

        Mode mode;
        DVector2 destination; // Current movement target.
        Entity target; // Current attack target.
        bool movingToTarget; // Cleared when attackDistance is reached.

        DReal turretRotation;

        // Firing cycle:
        //   10 Await trigger
        //   20 Fire left
        //   30 delay
        //   40 Fire right
        //   50 delay
        //   60 goto 10
        enum FireCycle {
                READY, FIREDLEFT, FIREDRIGHT
        }

        FireCycle fireCycle;
        DReal fireDelayTime;

        DReal barrelDelay = (DReal)1 / 5; // Delay between firing left & right barrels.
        DReal barrelRecycleTime = 2; // Delay before refiring one barrel.

        void TurnTurret(DReal targetAngle) {
                turretRotation = Utility.CalculateNewAngle(turretRotation, targetAngle, turretTurnSpeed);
        }

        void FireOneBarrel(int sign, GameObject barrel) {
                barrel.SendMessage("Fire");
                ComSat.SpawnProjectile(entity, projectilePrefab, entity.position, entity.rotation + turretRotation);
        }

        void Fire() {
                if(fireCycle != FireCycle.READY) {
                        return;
                }
                // Fire left.
                fireCycle = FireCycle.FIREDLEFT;
                fireDelayTime = barrelDelay;
                FireOneBarrel(+1, leftBarrel);
        }

        void TickUpdate() {
                if(mode == Mode.ATTACK && target == null) {
                        target = null;
                        mode = Mode.IDLE;
                }

                if(mode == Mode.ATTACK) {
                        var distVec = target.position - entity.position;
                        var dist = distVec.magnitude;
                        var targetTurretAngle = DReal.Mod(DVector2.ToAngle(distVec) - entity.rotation, DReal.TwoPI);

                        // Turn turret to point at target when close.
                        if(dist < attackRange * 2) {
                                TurnTurret(targetTurretAngle);
                        } else {
                                TurnTurret(0);
                        }

                        if(dist < attackDistance) {
                                // Close enough.
                                movingToTarget = false;
                        } else if(movingToTarget || (dist >= attackRange)) {
                                movingToTarget = true;
                                // Approach target.
                                vehicle.MoveTowards(target.position);
                        }

                        // Fire when in range and pointing the gun at the target.
                        if(dist < attackRange && targetTurretAngle == turretRotation) {
                                Fire();
                        }
                } else if(mode == Mode.MOVE) {
                        // Move towards.
                        if((destination - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                                // Close enough.
                                mode = Mode.IDLE;
                        } else {
                                vehicle.MoveTowards(destination);
                        }
                        TurnTurret(0);
                } else if(mode == Mode.IDLE) {
                        TurnTurret(0);
                }

                if(fireCycle != FireCycle.READY) {
                        fireDelayTime -= ComSat.tickRate;
                        if(fireDelayTime <= 0) {
                                if(fireCycle == FireCycle.FIREDLEFT) {
                                        // Fire right.
                                        fireCycle = FireCycle.FIREDRIGHT;
                                        FireOneBarrel(-1, rightBarrel);
                                        // This is enough time for the left barrel to recycle.
                                        fireDelayTime = barrelRecycleTime - barrelDelay;
                                } else if(fireCycle == FireCycle.FIREDRIGHT) {
                                        // cycle complete.
                                        fireCycle = FireCycle.READY;
                                }
                        }
                }
        }

        void Attack(Entity target) {
                if(target == entity) {
                        return;
                }
                mode = Mode.ATTACK;
                this.target = target;
                this.movingToTarget = false;
        }

        void Move(DVector2 location) {
                mode = Mode.MOVE;
                target = null;
                destination = location;
        }

        void Update() {
                // Always update local rotation.
                // Touching the world rotation causes fighting with Entity.
                turretMesh.transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(turretRotation), Vector3.up);
        }

        void OnDrawGizmosSelected() {
                // Projectile spawn location & stuff.
                Gizmos.color = Color.red;
                Vector3 turretPosition = new Vector3((float)turretAttachPoint.x,
                                                     0,
                                                     (float)turretAttachPoint.y);
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale) *
                        Matrix4x4.TRS(turretPosition, Quaternion.AngleAxis((float)DReal.Degrees(turretRotation), Vector3.up), transform.lossyScale);
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawWireSphere(new Vector3(0,0,0), 0.5f);
                Gizmos.DrawWireCube(new Vector3((float)turretSeperation, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));
                Gizmos.DrawWireCube(new Vector3(-(float)turretSeperation, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));

        }
}
