using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PowerSink : MonoBehaviour {
        public int maximumUsage;
        public int currentUsage { get; private set; }
        private Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
        }

        void TickUpdate() {
                currentUsage = 0;

                var sources = ComSat.FindEntitiesWithinRadius<PowerSource>(entity.position, DReal.MaxValue, getRadius: s => s.radius)
                        .Where(e => e.GetComponent<Entity>().team == entity.team);
                currentUsage = Math.Min(sources.Sum(s => s.currentPower), maximumUsage);
        }
}
