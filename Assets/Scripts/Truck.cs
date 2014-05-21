using UnityEngine;
using System.Collections;

[RequireComponent (typeof(Vehicle))]
[RequireComponent (typeof(Entity))]
public class Truck : MonoBehaviour {
        bool moving;
        DVector2 destination;

        // This close is close enough.
        static DReal sqrPositioningAccuracy = (DReal)1 / 100;

        Vehicle motor;
        Entity entity;

        public Texture2D constructionIcon;
        public GameObject constructionPrefab;

        void Awake() {
                moving = false;
                motor = GetComponent<Vehicle>();
                entity = GetComponent<Entity>();
        }

        void TickUpdate() {
                if(moving) {
                        if((destination - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                                // Close enough.
                                moving = false;
                        } else {
                                motor.MoveTowards(destination);
                        }
                }
        }

        void Move(DVector2 location) {
                Debug.Log(this + " moving to " + location);
                moving = true;
                destination = location;
        }

        void UIAction(int what) {
                ComSat.Instantiate(constructionPrefab, entity.team, entity.position, entity.rotation);
                Destroy(gameObject);
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
