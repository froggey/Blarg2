using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity), typeof(PowerSink))]
public class Mine : MonoBehaviour {
        public ResourceType resource;
        public int mineRate;

        private Entity entity;
        private PowerSink powerSink;
        private ResourceSource source;
        private DReal timer;

        void Start() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                powerSink = GetComponent<PowerSink>();
                source = Utility.GetThingAt<ResourceSource>(entity.position);
                source.hasMine = true;
        }

        void TickUpdate() {
                timer += ComSat.tickRate;
                if (entity.team != -1 && timer > 1) {
                        var amount = Math.Min(mineRate + powerSink.currentUsage, source.amount);
                        ComSat.AddResource(entity.team, resource, amount);
                        source.amount -= amount;
                }
                timer %= 1;
        }

        void OnDestroy() {
                source.hasMine = false;
        }
}
