using System;
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

        void OnDrawGizmos() {
                if (!entity.isSelected)
                        return;

                var oldColour = Gizmos.color;
                var c = Color.cyan;
                c.a = 0.25f;
                Gizmos.color = c;

                Gizmos.DrawSphere(
                        new Vector3(transform.position.x, Terrain.activeTerrain.SampleHeight(transform.position), transform.position.z),
                        radius);

                Gizmos.color = oldColour;
        }
}
