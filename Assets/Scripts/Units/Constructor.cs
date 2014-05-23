using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Entity))]
public class Constructor : MonoBehaviour {
        Entity entity;

        public Texture2D constructionIcon;
        public GameObject constructionPrefab;

        void Awake() {
                entity = GetComponent<Entity>();
        }

        void UIAction(int what) {
                ComSat.SpawnEntity(constructionPrefab, entity.team, entity.position, entity.rotation);
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

                if(GUI.Button(new Rect(10, 10, 64, 64), constructionIcon)) {
                        ComSat.IssueUIAction(entity, 0);
                }
        }
}
