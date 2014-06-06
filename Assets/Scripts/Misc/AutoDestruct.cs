using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// DO NOT USE WITH ENTITES OR PROJECTILES!
public class AutoDestruct : MonoBehaviour {
        public float destroyTime = 0.0f;

        void OnEnable() {
                StartCoroutine(TimedDestruction());
        }

        IEnumerator TimedDestruction() {
                yield return new WaitForSeconds(destroyTime);
                if(GetComponent<PooledObject>() != null) {
                        ObjectPool.For(GetComponent<PooledObject>().prototype).Uninstantiate(gameObject);
                } else {
                        Destroy(gameObject);
                }
    }
}
