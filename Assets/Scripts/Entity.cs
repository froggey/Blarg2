using UnityEngine;
using System.Collections;

public class Entity : MonoBehaviour {
        public DVector2 position;
        public DReal rotation;

        void Awake() {
                ComSat.EntityCreated(this);
        }
        void OnDestroy() {
                ComSat.EntityDestroyed(this);
        }

        void Update() {
                transform.localPosition = new Vector3((float)position.y,
                                                      transform.localPosition.y,
                                                      (float)position.x);
                transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(rotation), Vector3.up);
        }
}
