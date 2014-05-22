using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent (typeof(Entity))]
public class Barracks : MonoBehaviour {
        public GameObject[] prefabs;
        public Texture2D[] icons;

        public int buildTime;
        private DReal delay;

        Entity entity;

        void Awake() {
                entity = GetComponent<Entity>();
        }

        void TickUpdate() {
                if(delay > 0) {
                        delay -= ComSat.tickRate;
                }
        }

        void UIAction(int what) {
                if(delay > 0 || what < 0 || what >= prefabs.Length) {
                        return;
                }

                print("Build new " + prefabs[what]);

                var rotation = ComSat.RandomRange(0, DReal.TwoPI);
                var offset = DVector2.FromAngle(rotation) * ComSat.RandomRange(entity.collisionRadius + 5, entity.collisionRadius + 15);

                ComSat.SpawnEntity(prefabs[what], entity.team, entity.position + offset, rotation);
                delay = buildTime;
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

                for(int i = 0; i < prefabs.Length; ++i) {
                        if(GUI.Button(new Rect(10, 45 + i * 74, 64, 64), icons[i])) {
                                ComSat.IssueUIAction(entity, i);
                        }
                }
        }
}
