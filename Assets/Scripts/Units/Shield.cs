using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity), typeof(PowerSink))]
public class Shield : MonoBehaviour {
        public int radius;
        public Renderer fieldRenderer;
        public float glowLength;
        public Color glowColour;

        Entity entity;
        PowerSink powerSink;
        float glowTime;
        Color originalColour;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                powerSink = GetComponent<PowerSink>();

                originalColour = fieldRenderer.material.color;
        }

        void TickUpdate() {
                if(!powerSink.Powered()) {
                        return;
                }

                foreach(var p in ComSat.FindAllEntitiesWithinRadius<Projectile>(entity.position, radius, entity.team)) {
                        if(p.kind != Projectile.Kind.KINETIC) {
                                continue;
                        }

                        glowTime = glowLength;

                        p.speed /= 30 * ComSat.tickRate;
                }
        }

        void Update() {
                fieldRenderer.enabled = powerSink.Powered();
                if(glowTime <= 0) {
                        return;
                }

                var time = glowTime / glowLength;
                fieldRenderer.material.color = Color.Lerp(originalColour, glowColour, time);

                glowTime -= Time.deltaTime;
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(new Vector3(0,0,0), radius);
        }
}
