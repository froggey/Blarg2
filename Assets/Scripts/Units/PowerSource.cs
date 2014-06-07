﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class PowerSource : MonoBehaviour {
        public int maximumPower;
        public int currentPower { get; private set; } // TODO use this to make solar collectors dependent on light levels, etc.
        public int radius;

        private Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
        }

        void TickUpdate() {
                currentPower = maximumPower;
        }

        void OnDrawGizmosSelected() {
                var oldColour = Gizmos.color;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, radius);
                Gizmos.color = oldColour;
        }
}