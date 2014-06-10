using UnityEngine;
using System.Linq;
using System.Collections;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(CombatVehicle))]
[RequireComponent(typeof(AudioSource))]
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
        private CombatVehicle combatVehicle;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                combatVehicle = GetComponent<CombatVehicle>();

                fireCycle = FireCycle.READY;
        }

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
                audio.PlayOneShot(audio.clip);
                ComSat.SpawnEntity(entity, projectilePrefab,
                                   entity.position, entity.rotation,
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
                FireOneBarrel(+1);
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                
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

        void Update() {
                if(combatVehicle.mode == CombatVehicle.Mode.ATTACK && ComSat.EntityExists(combatVehicle.target)) {
                        laser.enabled = true;
                        laser.SetPosition(0, laserOrigin.transform.position);
                        laser.SetPosition(1, combatVehicle.target.transform.position);
                } else {
                        laser.enabled = false;
                }
        }

        void TurnTurret(DReal angle) {
                entity.rotation = angle;
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
