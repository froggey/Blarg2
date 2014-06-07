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

                var sources = ComSat.currentInstance.worldEntityCollisionCache
                        .Select(e => new { entity = e, source = e.GetComponent<PowerSource>() })
                        .Where(e => e.source != null &&
                                (e.entity.position - entity.position).sqrMagnitude <= e.source.radius * e.source.radius);
                currentUsage = Math.Min(sources.Sum(s => s.source.currentPower), maximumUsage);
        }
}
