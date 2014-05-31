using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Lifetime : MonoBehaviour {
        public int lifetime;
        public DReal age;

        Entity entity;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = gameObject.GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                age = 0;
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                age += ComSat.tickRate;
                if(age >= lifetime) {
                        ComSat.DestroyEntity(entity);
                }
        }
}
