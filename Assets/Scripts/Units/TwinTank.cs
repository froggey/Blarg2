using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(CombatVehicle))]
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
        private CombatVehicle combatVehicle;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                combatVehicle = GetComponent<CombatVehicle>();

                turretRotation = 0;

                fireCycle = FireCycle.READY;
        }

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
                turretRotation = targetAngle;
        }

        void FireOneBarrel(int sign, GameObject barrel) {
                if(ComSat.RateLimit()) {
                        barrel.SendMessage("Fire");
                }
                ComSat.SpawnEntity(entity, projectilePrefab,
                                   entity.position, entity.rotation + turretRotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && ComSat.EntityExists(combatVehicle.target)) {
                                                   proj.target = combatVehicle.target;
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
                FireOneBarrel(+1, leftBarrel);
        }
        
        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
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
