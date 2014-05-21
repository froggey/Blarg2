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
                ComSat.Instantiate(prefabs[what], entity.team, entity.position, entity.rotation);
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

                for(int i = 0; i < prefabs.Length; ++i) {
                        if(GUI.Button(new Rect(10, 10 + i * 74, 64, 64), icons[i])) {
                                ComSat.IssueUIAction(entity, i);
                        }
                }
        }
}
