using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PowerSource : MonoBehaviour {
        public int maximumPower;
        public int currentPower { get; private set; } // TODO use this to make solar collectors dependent on light levels, etc.

        private Entity entity;
        private PowerManager powerMan;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                powerMan = FindObjectOfType<PowerManager>();
                powerMan.AddSource(this);
        }

        void TickUpdate() {
                currentPower = maximumPower;
        }
}
