using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity), typeof(SimpleMovable))]
public class Orb : MonoBehaviour {
        private Entity entity;
        private SimpleMovable motor;
        private Entity[] targets;
        private Entity target;
        private int damageDelay;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                motor = GetComponent<SimpleMovable>();
        }

        void TickUpdate() {
                if (!ComSat.EntityExists(target)) PickNewTarget();
                if (target == null) return;
                if ((target.position - entity.position).sqrMagnitude < 4 + target.collisionRadius * target.collisionRadius && damageDelay-- <= 0) {
                        target.Damage(1);
                        damageDelay = 3;
                }
        }

        void Attack(Entity[] targets) {
                this.targets = targets;
                PickNewTarget();
                motor.Move(target.position);
        }
        
        private void PickNewTarget() {
                if (targets == null) targets = new Entity[] {};
                targets = targets.Where(t => t != null).OrderBy(t => (t.position - entity.position).sqrMagnitude).ToArray();
                target = targets.FirstOrDefault();
                if (target != null) motor.Move(target.position);
        }
}
