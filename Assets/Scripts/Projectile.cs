using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour {
        public DVector2 position;
        public DReal rotation;
        public DReal height;

        public int team = 0;
        public Entity origin; // who spawned this.

        void Update() {
                transform.localPosition = new Vector3((float)position.y,
                                                      (float)height,
                                                      (float)position.x);
                transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(rotation), Vector3.up);
        }
}
