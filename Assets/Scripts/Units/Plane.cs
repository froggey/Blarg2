using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(Vehicle))]
public class Plane : MonoBehaviour {
        public GameObject missilePrefab;
        public GameObject gunPrefab;

        private Entity entity;
        private Vehicle vehicle;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                vehicle = GetComponent<Vehicle>();

                missilesLoaded = maxMissiles;
                destination = entity.position;
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

        bool explicitMove;
        DVector2 destination; // Current movement target.
        Entity target; // Current attack target.

        void FireGun() {
                if(gunRecycleDelay > 0) return;

                ComSat.SpawnEntity(entity, gunPrefab,
                                   entity.position, entity.rotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && target != null) {
                                                   proj.target = target;
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
                                           if(proj != null && target != null) {
                                                   proj.target = target;
                                           }
                                   });

                missileRecycleDelay = missileRecycleTime;
                missilesLoaded -= 1;
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(target == null) {
                        target = null;
                        if(!explicitMove) {
                                // Chill out here.
                                destination = entity.position;
                                explicitMove = true;
                        }
                        gunFireSound.Stop();
                } else {
                        destination = target.position;

                        var dist = target.position - entity.position;
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
                }

                vehicle.MoveTowards(destination);

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

        void Attack(Entity target) {
                ComSat.Trace(this, "Attack");
                if(target == entity) {
                        return;
                }
                this.target = target;
                explicitMove = false;
        }

        void Move(DVector2 location) {
                ComSat.Trace(this, "Move");
                target = null;
                destination = location;
                explicitMove = true;
        }
}
