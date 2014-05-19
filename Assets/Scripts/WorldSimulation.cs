using UnityEngine;
using System.Collections.Generic;

public class WorldSimulation : MonoBehaviour {
        public void TickUpdate() {
        }

        // WorldSimulation drives ComSat to some degree, acting as a bridge
        // between MonoBehaviour and ComSat.
        void Awake() {
                ComSat.LevelLoad(this);
        }

        void OnDestroy() {
                ComSat.LevelUnload();
        }

        void Update() {
                ComSat.Update();
        }
}
