using UnityEngine;
using System.Collections;

// Pin the Y axis to the terrain.
[ExecuteInEditMode]
public class PinToTerrain : MonoBehaviour {
        void Update() {
                if(Terrain.activeTerrain) {
                        transform.position = new Vector3(transform.position.x,
                                                         Terrain.activeTerrain.SampleHeight(transform.position),
                                                         transform.position.z);
                }
        }
}
