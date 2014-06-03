using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(AudioSource))]
public class Turret : MonoBehaviour {
        private Entity entity;

        DReal turretRotation;
        Entity target; // Current attack target.

        public DReal attackRange = 60; // Maximum firing range.
        static DReal turretTurnSpeed = DReal.Radians(727); // radians per second

        public DReal projectileSpawnDistance = 3;
        public DVector2 turretAttachPoint = new DVector2(0, 0);
        public GameObject turretMesh;
        public GameObject projectilePrefab;
        public GameObject turretBarrel;

        public DReal barrelRecycleTime = (DReal)1 / 10; // Delay before refiring one barrel.

        private DReal fireDelay;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);

                target = null;
                turretRotation = 0;

                fireDelay = 0;
        }

        void TurnTurret(DReal targetAngle) {
                turretRotation = Utility.CalculateNewAngle(turretRotation, targetAngle, turretTurnSpeed);
        }

        void Fire() {
                if(fireDelay > 0) {
                        return;
                }

                fireDelay = barrelRecycleTime;
                ComSat.SpawnEntity(entity, projectilePrefab,
                                   entity.position, entity.rotation + turretRotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && ComSat.EntityExists(target)) {
                                                   proj.target = target;
                                           }
                                   });
                turretBarrel.SendMessage("Fire");
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(!ComSat.EntityExists(target)) {
                        // Magic. Destroyed GameObjects compare against null.
                        // Explicitly set to null to avoid keeping it around.
                        target = null;
                        audio.Stop();

                        // Search for victims.
                        target = ComSat.FindEntityWithinRadius(entity.position, attackRange, entity.team);
                        if(target != null) {
                                audio.Play();
                        }
                } else {
                        var dp = target.position - entity.position;

                        DReal targetTurretAngle;

                        var projectileProjectile = projectilePrefab.GetComponent<Projectile>();
                        if(projectileProjectile != null) {
                                var aimSpot = Utility.PredictShot(entity.position, projectileProjectile.initialSpeed,
                                                                  target.position, target.velocity);
                                targetTurretAngle = DReal.Mod(DVector2.ToAngle(aimSpot - entity.position) - entity.rotation, DReal.TwoPI);
                        } else {
                                targetTurretAngle = DReal.Mod(DVector2.ToAngle(dp) - entity.rotation, DReal.TwoPI);
                        }

                        // Turn turret to point at target.
                        TurnTurret(targetTurretAngle);
                        // Fire when pointing the gun at the target.
                        if(targetTurretAngle == turretRotation) {
                                Fire();
                        }

                        // Stop shooting when out of range.
                        if(dp.sqrMagnitude >= attackRange * attackRange) {
                                audio.Stop();
                                target = null;
                        }
                }

                if(fireDelay > 0) {
                        fireDelay -= ComSat.tickRate;
                }
        }

        void Update() {
                // Always update local rotation.
                // Touching the world rotation causes fighting with Entity.
                turretMesh.transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(turretRotation), Vector3.up);
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, (float)attackRange);

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
