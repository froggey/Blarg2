using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PowerSource : MonoBehaviour, ISabotagable {
        public int maximumPower;
        public int currentPower { get; private set; } // TODO use this to make solar collectors dependent on light levels, etc.

        private Entity entity;
        private PowerManager powerMan;

        // Sabotage time remaining.
        DReal sabotageTime;
        // Sabotage lasts this long.
        public int sabotageRepairTime;
        // Sabotaged PowerSource produces this much power.
        public int sabotageMaxPower;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                powerMan = FindObjectOfType<PowerManager>();
                powerMan.AddSource(this);
        }

        void TickUpdate() {
                currentPower = (sabotageTime > 0) ? sabotageMaxPower : maximumPower;

                if(sabotageTime > 0) {
                        sabotageTime -= ComSat.tickRate;
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
}
