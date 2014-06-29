using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity), typeof(PowerSink))]
public class Shield : MonoBehaviour, ISabotagable {
        public int radius;
        public Renderer fieldRenderer;
        public float glowLength;
        public Color glowColour;

        Entity entity;
        PowerSink powerSink;
        float glowTime;
        Color originalColour;

        // Sabotage time remaining.
        DReal sabotageTime;
        // Sabotage lasts this long.
        public int sabotageRepairTime;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                powerSink = GetComponent<PowerSink>();

                originalColour = fieldRenderer.material.color;
        }

        void TickUpdate() {
                if(sabotageTime > 0) {
                        sabotageTime -= ComSat.tickRate;
                        return;
                }

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

        public void Sabotage() {
                sabotageTime += sabotageRepairTime;
        }

        private bool isSelected;

        private void OnSelected() {
                isSelected = true;
        }

        private void OnUnselected() {
                isSelected = false;
        }

        void OnGUI() {
                if(!isSelected) return;

                if(sabotageTime > 0) {
                        GUI.Box(new Rect(84, Camera.main.pixelHeight - 100, 100, 25), "Sabotaged! " + Mathf.Ceil((float)sabotageTime));
                }
        }

        void Update() {
                fieldRenderer.enabled = (sabotageTime <= 0) && powerSink.Powered();
                if(glowTime <= 0) {
                        return;
                }

                var time = glowTime / glowLength;
                fieldRenderer.material.color = Color.Lerp(originalColour, glowColour, time);

                glowTime -= Time.deltaTime;
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, radius);
        }
}
