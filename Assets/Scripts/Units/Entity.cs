using UnityEngine;
using System.Collections;

public class Entity : MonoBehaviour {
        public DVector2 position;
        public DReal rotation;
        public int team;
        public Entity origin; // who spawned this.

        public int maxHealth;
        public int health;

        // This is super dumb.
        // The inspector can't display DReals, so expose the collisionRadius as a fraction.
        public int collisionRadiusNumerator;
        public int collisionRadiusDenominator = 1;

        public DReal collisionRadius;

        public GameObject baseMesh;

        void Awake() {
                collisionRadius = (DReal)collisionRadiusNumerator / collisionRadiusDenominator;

                health = maxHealth;
        }

        void Start() {
                if(baseMesh && team != 0) {
                        baseMesh.renderer.material.color = Utility.TeamColour(team);
                }
        }

        void Update() {
                transform.localPosition = new Vector3((float)position.y,
                                                      transform.localPosition.y,
                                                      (float)position.x);
                transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(rotation), Vector3.up);
        }

        public void Damage(int damage) {
                health -= damage;
                if(health <= 0) {
                        ComSat.DestroyEntity(this);
                }
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, (float)collisionRadiusNumerator / collisionRadiusDenominator);
        }
}
