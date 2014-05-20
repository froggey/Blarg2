using UnityEngine;
using System.Collections;

public class Entity : MonoBehaviour {
        public DVector2 position;
        public DReal rotation;
        public int team;

        public int maxHealth;
        private int health;

        public DReal collisionRadius;

        void Awake() {
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
}
