using UnityEngine;
using System.Collections.Generic;

public class AutoDestruct : MonoBehaviour {
        public float destroyTime = 0.0f;

        void Start() {
                Destroy(gameObject, destroyTime);
        }
}
