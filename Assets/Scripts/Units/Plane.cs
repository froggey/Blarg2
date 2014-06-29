using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(CombatVehicle))]
public class Plane : MonoBehaviour {
        public GameObject missilePrefab;
        public GameObject gunPrefab;

        private Entity entity;
        private CombatVehicle combatVehicle;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                combatVehicle = GetComponent<CombatVehicle>();

                missilesLoaded = maxMissiles;
        }

        public int maxMissiles;
        public int missileReloadDelay;
        public int missilesLoaded;
        public DReal missileRecycleTime = (DReal)1 / 4;
        public AudioSource missileFireSound;

        public DReal gunRecycleTime = (DReal)1 / 10;
        public AudioSource gunFireSound;

        public int missileRange;
        public int gunRange;

        DReal missileReloadTime;
        DReal missileRecycleDelay;
        DReal gunRecycleDelay;

        void FireGun() {
                if(gunRecycleDelay > 0) return;

                ComSat.SpawnEntity(entity, gunPrefab,
                                   entity.position, entity.rotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && ComSat.EntityExists(combatVehicle.target)) {
                                                   proj.target = combatVehicle.target;
                                           }
                                   });

                gunRecycleDelay = gunRecycleTime;
        }

        void FireMissile() {
                if(missileRecycleDelay > 0) return;
                if(missilesLoaded <= 0) return;

                missileFireSound.PlayOneShot(missileFireSound.clip);
                ComSat.SpawnEntity(entity, missilePrefab,
                                   entity.position, entity.rotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && ComSat.EntityExists(combatVehicle.target)) {
                                                   proj.target = combatVehicle.target;
                                           }
                                   });

                missileRecycleDelay = missileRecycleTime;
                missilesLoaded -= 1;
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");

                if (combatVehicle.mode == CombatVehicle.Mode.IDLE) {
                        combatVehicle.mode = CombatVehicle.Mode.MOVE;
                        combatVehicle.destination = entity.position;
                }

                if (ComSat.EntityExists(combatVehicle.target)) {
                        var dist = combatVehicle.target.position - entity.position;
                        var sqrDist = dist.sqrMagnitude;
                        var targetAngle = DReal.Mod(DVector2.ToAngle(dist), DReal.TwoPI);

                        // Get targetAngle within +/- pi of entity.rotation.
                        if(targetAngle < entity.rotation - DReal.PI) {
                                targetAngle += DReal.TwoPI;
                        } else if(targetAngle > entity.rotation + DReal.PI) {
                                targetAngle -= DReal.TwoPI;
                        }

                        if(sqrDist < missileRange * missileRange) {
                                FireMissile();
                        }

                        if(sqrDist < gunRange * gunRange && DReal.Abs(entity.rotation - targetAngle) < 1) {
                                FireGun();
                                if(!gunFireSound.isPlaying) {
                                        gunFireSound.Play();
                                }
                        } else {
                                gunFireSound.Stop();
                        }
                } else {
                        gunFireSound.Stop();
                }

                if(missilesLoaded < maxMissiles) {
                        if(missileReloadTime <= 0) {
                                missileReloadTime = missileReloadDelay;
                        } else {
                                missileReloadTime -= ComSat.tickRate;
                                if(missileReloadTime <= 0) {
                                        missilesLoaded += 1;
                                }
                        }
                }

                if(missileRecycleDelay > 0) {
                        missileRecycleDelay -= ComSat.tickRate;
                }

                if(gunRecycleDelay > 0) {
                        gunRecycleDelay -= ComSat.tickRate;
                }
        }

        // ignore these signals, planes have special firing behaviour because two guns and stuff
        void TurnTurret(DReal angle) {}
        void Fire() {}
}
