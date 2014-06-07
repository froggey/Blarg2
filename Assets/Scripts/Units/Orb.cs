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
                foreach(var ent in ComSat.currentInstance.worldEntityCollisionCache) {
                        if (ent == entity) continue;
                        if(ent.team != entity.team && (ent.position - entity.position).sqrMagnitude < ent.collisionRadius * ent.collisionRadius + 4) {
                                ent.Damage(5);
                        }
                }
        }

        void Attack(Entity target) {
                motor.Move(target.position);
        }
}
