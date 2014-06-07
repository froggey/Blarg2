using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity), typeof(SimpleMovable))]
public class Orb : MonoBehaviour {
        private Entity entity;
        private SimpleMovable motor;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                motor = GetComponent<SimpleMovable>();
        }

        void TickUpdate() {
                foreach(var ent in ComSat.FindEntitiesWithinRadius(entity.position, 4, entity.team)) {
                        ent.Damage(5);
                }
        }

        void Attack(Entity target) {
                motor.Move(target.position);
        }
}
