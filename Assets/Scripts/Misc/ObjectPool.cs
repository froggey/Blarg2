using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class ObjectPool : IDisposable {
        private Stack<GameObject> objects;
        private GameObject prototype;

        public ObjectPool(GameObject proto) {
                objects = new Stack<GameObject>(512);
                prototype = proto;
        }

        public GameObject Instantiate() {
                if (objects.Any()) {
                        var obj = objects.Pop();
                        obj.SetActive(true);
                        return obj;
                } else {
                        var go = GameObject.Instantiate(prototype) as GameObject;
                        go.GetComponent<PooledObject>().prototype = prototype;
                        return go;
                }
        }

        public GameObject Instantiate(Vector3 position, Quaternion rotation) {
                var obj = Instantiate();
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                return obj;
        }

        public void Uninstantiate(GameObject obj) {
                obj.SetActive(false);
                objects.Push(obj);
        }

        public void Dispose() {
                foreach (var obj in objects) {
                        GameObject.Destroy(obj);
                }
        }

        public int Count { get { return objects.Count; } }

        public static readonly Dictionary<GameObject, ObjectPool> Pools = new Dictionary<GameObject, ObjectPool>();

        public static ObjectPool For(GameObject prototype) {
                if (Pools.ContainsKey(prototype)) {
                        return Pools[prototype];
                } else {
                        return (Pools[prototype] = new ObjectPool(prototype));
                }
        }

        public static void FlushAll() {
                foreach (var pool in Pools.Values) {
                        pool.Dispose();
                }
                Pools.Clear();
        }
}
