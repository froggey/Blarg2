using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(Vehicle))]
public class ZMachine : MonoBehaviour {
        // Can't use GameObjects to set these up because floats.
        // Unity refuses to edit these because they're structs.
        // :(
        public DReal projectileSpawnDistance = 6;
        public DVector2 turretAttachPoint = new DVector2(0, 0);
        public GameObject turretObject;
        public GameObject barrelObject;
        public GameObject projectilePrefab;

        private Entity entity;
        private Vehicle vehicle;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                vehicle = GetComponent<Vehicle>();

                mode = Mode.IDLE;
                turretRotation = 0;
        }

        enum Mode {
                IDLE, MOVE, ATTACK
        }

        public int attackDistance = 150; // Try to stay this close.
        public int attackRange = 200; // Maximum firing range.
        static DReal turretTurnSpeed = DReal.Radians(400); // radians per second
        static DReal sqrPositioningAccuracy = (DReal)1 / 100;

        Mode mode;
        DVector2 destination; // Current movement target.
        Entity target; // Current attack target.
        bool movingToTarget; // Cleared when attackDistance is reached.

        DReal turretRotation;

        DReal fireDelayTime;

        public int barrelRecycleTime;

        void TurnTurret(DReal targetAngle) {
                turretRotation = Utility.CalculateNewAngle(turretRotation, targetAngle, turretTurnSpeed);
        }

        void Fire() {
                if(fireDelayTime > 0) {
                        return;
                }
                fireDelayTime = barrelRecycleTime;
                barrelObject.SendMessage("Fire");
                ComSat.SpawnEntity(entity, projectilePrefab,
                                   entity.position, entity.rotation + turretRotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && ComSat.EntityExists(target)) {
                                                   proj.target = target;
                                           }
                                   });
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(mode == Mode.ATTACK && !ComSat.EntityExists(target)) {
                        target = null;
                        mode = Mode.IDLE;
                        vehicle.Stop();
                }

                if(mode == Mode.ATTACK) {
                        var distVec = target.position - entity.position;
                        var dist = distVec.magnitude;

                        DReal targetTurretAngle;

                        var projectileProjectile = projectilePrefab.GetComponent<Projectile>();
                        if(projectileProjectile != null) {
                                var aimSpot = Utility.PredictShot(entity.position, projectileProjectile.initialSpeed,
                                                                  target.position, target.velocity);

                                targetTurretAngle = DReal.Mod(DVector2.ToAngle(aimSpot - entity.position) - entity.rotation, DReal.TwoPI);
                        } else {
                                targetTurretAngle = DReal.Mod(DVector2.ToAngle(distVec) - entity.rotation, DReal.TwoPI);
                        }

                        // Turn turret to point at target when close.
                        if(dist < attackRange * 2) {
                                TurnTurret(targetTurretAngle);
                        } else {
                                TurnTurret(0);
                        }

                        if(dist < attackDistance) {
                                // Close enough.
                                movingToTarget = false;
                                vehicle.Stop();
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
                                vehicle.Stop();
                        } else {
                                vehicle.MoveTowards(destination);
                        }
                        TurnTurret(0);
                } else if(mode == Mode.IDLE) {
                        TurnTurret(0);
                }

                if(fireDelayTime > 0) {
                        fireDelayTime -= ComSat.tickRate;
                }
        }

        void Attack(Entity target) {
                ComSat.Trace(this, "Attack");
                if(target == entity) {
                        return;
                }
                mode = Mode.ATTACK;
                this.target = target;
                this.movingToTarget = false;
        }

        void Move(DVector2 location) {
                ComSat.Trace(this, "Move");
                mode = Mode.MOVE;
                target = null;
                destination = location;
        }

        void Update() {
                // Always update local rotation.
                // Touching the world rotation causes fighting with Entity.
                turretObject.transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(turretRotation), Vector3.up);
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
                Gizmos.DrawWireCube(new Vector3(0, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));

        }
}
