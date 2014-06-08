using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PowerSink : MonoBehaviour {
        public int powerUsage;
        public bool poweredOn;
        public bool powerIsToggleableInGame;
        private Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
        }

        void SetPowerState(bool on) {
                poweredOn = on;
        }

        void OnGUI() {
                if (entity.isSelected && powerIsToggleableInGame) {
                        if (GUI.Button(new Rect(Camera.main.pixelWidth - 74, Camera.main.pixelHeight - 74, 64, 64), poweredOn ? "ON" : "OFF")) {
                                ComSat.IssueSetPowerState(entity, !poweredOn);
                        }
                }
        }
}
