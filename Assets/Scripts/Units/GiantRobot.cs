using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(Vehicle))]
public class GiantRobot : MonoBehaviour {
        // Can't use GameObjects to set these up because floats.
        // Unity refuses to edit these because they're structs.
        // :(
        public DReal projectileSpawnDistance = 6;
        public DVector2 turretAttachPoint = new DVector2(0, -(DReal)597 / 2048);
        public DReal turretSeperation = (DReal)1 / 3;
        public GameObject projectilePrefab;

        public GameObject laserOrigin;
        public LineRenderer laser;

        private Entity entity;
        private Vehicle vehicle;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(6, TickUpdate);
                vehicle = GetComponent<Vehicle>();

                mode = Mode.IDLE;

                fireCycle = FireCycle.READY;
        }

        enum Mode {
                IDLE, MOVE, ATTACK
        }

        static DReal attackDistance = 75; // Try to stay this close.
        static DReal attackRange = 100; // Maximum firing range.
        static DReal sqrPositioningAccuracy = (DReal)1 / 100;

        Mode mode;
        DVector2 destination; // Current movement target.
        Entity target; // Current attack target.
        bool movingToTarget; // Cleared when attackDistance is reached.

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

        void FireOneBarrel(int sign) {
                ComSat.SpawnEntity(entity, projectilePrefab,
                                   entity.position, entity.rotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && target != null) {
                                                   proj.target = target;
                                           }
                                   });
        }

        void Fire() {
                if(fireCycle != FireCycle.READY) {
                        return;
                }
                // Fire left.
                fireCycle = FireCycle.FIREDLEFT;
                fireDelayTime = barrelDelay;
                FireOneBarrel(+1);
        }

        void TickUpdate() {
                if(mode == Mode.ATTACK && target == null) {
                        target = null;
                        mode = Mode.IDLE;
                        vehicle.Stop();
                }

                if(mode == Mode.ATTACK) {
                        var distVec = target.position - entity.position;
                        var dist = distVec.magnitude;

                        if(dist < attackDistance) {
                                // Close enough.
                                movingToTarget = false;
                                vehicle.Stop();
                        } else if(movingToTarget || (dist >= attackRange)) {
                                movingToTarget = true;
                                // Approach target.
                                vehicle.MoveTowards(target.position);
                        }

                        // Fire when in range.
                        if(dist < attackRange) {
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
                }

                if(fireCycle != FireCycle.READY) {
                        fireDelayTime -= ComSat.tickRate;
                        if(fireDelayTime <= 0) {
                                if(fireCycle == FireCycle.FIREDLEFT) {
                                        // Fire right.
                                        fireCycle = FireCycle.FIREDRIGHT;
                                        FireOneBarrel(-1);
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
                if(mode == Mode.ATTACK && target != null) {
                        laser.enabled = true;
                        laser.SetPosition(0, laserOrigin.transform.position);
                        laser.SetPosition(1, target.transform.position);
                } else {
                        laser.enabled = false;
                }
        }

        void OnDrawGizmosSelected() {
                // Projectile spawn location & stuff.
                Gizmos.color = Color.red;
                Vector3 turretPosition = new Vector3((float)turretAttachPoint.x,
                                                     0,
                                                     (float)turretAttachPoint.y);
                Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale) *
                        Matrix4x4.TRS(turretPosition, Quaternion.AngleAxis(0, Vector3.up), transform.lossyScale);
                Gizmos.matrix = rotationMatrix;
                Gizmos.DrawWireSphere(new Vector3(0,0,0), 0.5f);
                Gizmos.DrawWireCube(new Vector3((float)turretSeperation, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));
                Gizmos.DrawWireCube(new Vector3(-(float)turretSeperation, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));

        }
}
