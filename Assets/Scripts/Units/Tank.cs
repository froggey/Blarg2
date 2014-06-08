using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent (typeof(Entity))]
[RequireComponent (typeof(CombatVehicle))]
public class Tank : MonoBehaviour {
        // Can't use GameObjects to set these up because floats.
        // Unity refuses to edit these because they're structs.
        // :(
        public DReal projectileSpawnDistance = 6;
        public DVector2 turretAttachPoint = new DVector2(0, -(DReal)597 / 2048);
        public GameObject turretMesh;
        public GameObject barrel;

        DReal turretRotation;

        DReal fireDelayTime;

        DReal barrelRecycleTime = 2; // Delay before refiring one barrel.

        private Entity entity;
        private CombatVehicle combatVehicle;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                combatVehicle = GetComponent<CombatVehicle>();
        }

        void TurnTurret(DReal targetAngle) {
                turretRotation = targetAngle;
        }

        void Fire() {
                if(fireDelayTime > 0) {
                        return;
                }
                // Fire left.
                fireDelayTime = barrelRecycleTime;

                if(ComSat.RateLimit()) {
                        barrel.SendMessage("Fire");
                }
                ComSat.SpawnEntity(entity, combatVehicle.projectilePrefab,
                                   entity.position, entity.rotation + turretRotation,
                                   (Entity ent) => {
                                           var proj = ent.gameObject.GetComponent<Projectile>();
                                           if(proj != null && ComSat.EntityExists(combatVehicle.target)) {
                                                   proj.target = combatVehicle.target;
                                           }
                                   });
        }

        void TickUpdate() {
                if(fireDelayTime > 0) {
                        fireDelayTime -= ComSat.tickRate;
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
                Gizmos.DrawWireCube(new Vector3(0, 0, (float)projectileSpawnDistance),
                                    new Vector3(0.5f, 0.5f, 0.5f));
        }
}
