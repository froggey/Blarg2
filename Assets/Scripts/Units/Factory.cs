using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent (typeof(Entity))]
public class Factory : MonoBehaviour {
        public Entity[] prefabs;

        private DReal delay;

        private Queue<int> buildQueue;

        Entity entity;

        private const int clearQueue = -1;

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(2, TickUpdate);
                buildQueue = new Queue<int>();
        }

        void TickUpdate() {
                if(delay > 0) {
                        delay -= ComSat.tickRate;
                }
                if(delay <= 0 && buildQueue.Any()) {
                        // Timer expired and we're building something.
                        var buildMe = buildQueue.Dequeue();
                        print("Build new " + prefabs[buildMe]);

                        var rotation = ComSat.RandomRange(0, DReal.TwoPI);
                        var offset = DVector2.FromAngle(rotation) * ComSat.RandomRange(entity.collisionRadius + 5, entity.collisionRadius + 15);

                        ComSat.SpawnEntity(entity, prefabs[buildMe].gameObject, entity.position + offset, rotation);
                }
                if(delay <= 0 && buildQueue.Any()) {
                        delay = prefabs[buildQueue.Peek()].buildTime;
                }
        }

        void UIAction(int what) {
                if(what == clearQueue) {
                        buildQueue.Clear();
                        delay = 0;
                }
                else if(what >= 0 && what < prefabs.Length) {
                        buildQueue.Enqueue(what);
                        delay = prefabs[buildQueue.Peek()].buildTime;
                }
        }

        private bool isSelected;

        private void OnSelected() {
                isSelected = true;
        }

        private void OnUnselected() {
                isSelected = false;
        }

        private void OnGUI() {
                if(!isSelected) return;

                if(delay > 0) {
                        GUI.Box(new Rect(10, 10, 64, 25), ((int)Mathf.Ceil((float)delay)).ToString());
                } else {
                        GUI.Box(new Rect(10, 10, 64, 25), "Ready");
                }

                var building = buildQueue.Any() ? buildQueue.Peek() : -1;

                for(int i = 0; i < prefabs.Length; ++i) {
                        GUI.backgroundColor = (i == building && Time.time % 1 < 0.5f) ? Color.yellow : Color.white;

                        if(GUI.Button(new Rect(10, 45 + i * 74, 64, 64), prefabs[i].buildIcon)) {
                                ComSat.IssueUIAction(entity, i);
                        }

                        GUI.backgroundColor = Color.white;

                        var queued = buildQueue.Count(qi => qi == i);
                        if(queued > 0) {
                                GUI.Label(new Rect(10, 45 + i * 74, 64, 24), queued.ToString());
                        }
                }

                if(buildQueue.Any() && GUI.Button(new Rect(10, 45 + prefabs.Length * 74, 64, 24), "Stop")) {
                        ComSat.IssueUIAction(entity, clearQueue);
                }
        }
}
