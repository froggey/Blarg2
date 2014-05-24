using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent (typeof(Entity))]
public class Factory : MonoBehaviour {
        public Entity[] prefabs;

        private DReal delay;
        private int buildMe;

        Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
                buildMe = -1;
        }

        void TickUpdate() {
                if(delay > 0) {
                        delay -= ComSat.tickRate;
                }
                if(delay <= 0 && buildMe != -1) {
                        // Timer expired and we're building something.
                        print("Build new " + prefabs[buildMe]);

                        var rotation = ComSat.RandomRange(0, DReal.TwoPI);
                        var offset = DVector2.FromAngle(rotation) * ComSat.RandomRange(entity.collisionRadius + 5, entity.collisionRadius + 15);

                        ComSat.SpawnEntity(entity, prefabs[buildMe].gameObject, entity.position + offset, rotation);

                        buildMe = -1;
                }
        }

        void UIAction(int what) {
                if(delay > 0 || what < 0 || what >= prefabs.Length) {
                        return;
                }

                delay = prefabs[what].buildTime;
                buildMe = what;
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
                        GUI.backgroundColor = Color.red;
                        GUI.Box(new Rect(10, 10, 64, 25), ((int)Mathf.Ceil((float)delay)).ToString());
                } else {
                        GUI.Box(new Rect(10, 10, 64, 25), "Ready");
                }

                for(int i = 0; i < prefabs.Length; ++i) {
                        if(GUI.Button(new Rect(10, 45 + i * 74, 64, 64), prefabs[i].buildIcon)) {
                                ComSat.IssueUIAction(entity, i);
                        }
                }
        }
}
