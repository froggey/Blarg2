using UnityEngine;
using System.Collections;

public class Entity : MonoBehaviour {
        public DVector2 position;
        public DReal rotation;
        public int team;

        public int maxHealth;
        public int health;

        // This is super dumb.
        // The inspector can't display DReals, so expose the collisionRadius as a fraction.
        public int collisionRadiusNumerator;
        public int collisionRadiusDenominator;

        public DReal collisionRadius;

        void Awake() {
                collisionRadius = (DReal)collisionRadiusNumerator / collisionRadiusDenominator;

                ComSat.EntityCreated(this);

                health = maxHealth;
        }
        void OnDestroy() {
                ComSat.EntityDestroyed(this);
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
                        Destroy(gameObject);
                }
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, (float)collisionRadiusNumerator / collisionRadiusDenominator);
        }
}
