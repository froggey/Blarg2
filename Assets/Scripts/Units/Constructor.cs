using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Constructor : MonoBehaviour {
        Entity entity;

        public Entity constructionPrefab;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
        }

        void UIAction(int what) {
                ComSat.Trace(this, "UIAction");
                ComSat.SpawnEntity(constructionPrefab.gameObject, entity.team, entity.position, entity.rotation);
                ComSat.DestroyEntity(entity);
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

                // TODO: follow Entity.buildTime.
                if(GUI.Button(new Rect(10, 10, 64, 64), constructionPrefab.buildIcon)) {
                        ComSat.IssueUIAction(entity, 0);
                }
        }
}
