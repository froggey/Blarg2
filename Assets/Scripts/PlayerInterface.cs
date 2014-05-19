using UnityEngine;
using System.Collections;

public class PlayerInterface : MonoBehaviour {
        private GameObject selectedUnit;

        void Update() {
                if(Input.GetMouseButtonUp(0)) {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                        if(selectedUnit) {
                                selectedUnit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                                selectedUnit = null;
                        }

                        // Selectable units only.
                        RaycastHit hit;
                        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<8)) {
                                selectedUnit = hit.transform.gameObject;
                                selectedUnit.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
                        }
                }

                if(Input.GetMouseButtonUp(1) && selectedUnit) {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        // Shoot for units first.
                        RaycastHit hit;
                        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<8)) {
                                Entity target = hit.transform.gameObject.GetComponent<Entity>();
                                ComSat.IssueAttack(selectedUnit.GetComponent<Entity>(), target);
                        // Otherwise, do a move.
                        } else if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<9)) {
                                DVector2 point = new DVector2((DReal)hit.point.x, (DReal)hit.point.z);
                                ComSat.IssueMove(selectedUnit.GetComponent<Entity>(), point);
                        }
                }
        }
}
