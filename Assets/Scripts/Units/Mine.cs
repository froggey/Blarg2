using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity))]
public class Mine : MonoBehaviour {
        public ResourceType resource;

        private Entity entity;
        private ResourceSource source;

        void Start() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(11, TickUpdate);
                source = Utility.GetThingAt<ResourceSource>(entity.position);
                source.hasMine = true;
        }

        void TickUpdate() {
                if (entity.team != -1) {
                        var amount = Math.Min(source.mineRate, source.amount);
                        ComSat.AddResource(entity.team, source.resource, amount);
                        source.amount -= amount;
                }
        }

        void OnDestroy() {
                source.hasMine = false;
        }
}
