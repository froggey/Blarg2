using UnityEngine;
using System.Collections.Generic;

// DO NOT USE WITH ENTITES OR PROJECTILES!
public class AutoDestruct : MonoBehaviour {
        public float destroyTime = 0.0f;

        void Start() {
                Destroy(gameObject, destroyTime);
        }
}
