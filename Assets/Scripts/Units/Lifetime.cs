using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Lifetime : MonoBehaviour {
        public int lifetime;
        public DReal age;

        Entity entity;

        void Awake() {
                entity = gameObject.GetComponent<Entity>();
                entity.AddUpdateAction(0, TickUpdate);
                age = 0;
        }

        void TickUpdate() {
                age += ComSat.tickRate;
                if(age >= lifetime) {
                        ComSat.DestroyEntity(entity);
                }
        }
}
