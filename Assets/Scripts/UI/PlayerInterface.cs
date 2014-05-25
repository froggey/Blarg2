using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerInterface : MonoBehaviour {
        private bool disabledBecauseGUI = false;

        public Texture marqueeGraphics;
        public Color overlayColour;
        Vector2 marqueeOrigin;
        Rect marqueeRect;
        bool marqueeActive;

        List<GameObject> selectedUnits = new List<GameObject>();

        void OnGUI() {
                if(marqueeActive) {
                        GUI.color = overlayColour;
                        GUI.DrawTexture(marqueeRect, marqueeGraphics);
                }
        }

        Vector2 MousePosition() {
                // Must invert y. GUI & input y coordinates disagree.
                return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        }

        void LateUpdate() {
                if(!marqueeActive) {
                        // No raycasting when over a GUI widget.
                        if(GUIUtility.hotControl != 0) {
                                disabledBecauseGUI = true;
                        }
                        // Wait for mouse0 to go up, then wait a frame.
                        if(disabledBecauseGUI) {
                                if(Input.GetMouseButtonUp(0)) {
                                        disabledBecauseGUI = false;
                                }
                                return;
                        }
                }

                if(Input.GetButtonUp("Select")) {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                        foreach(var unit in selectedUnits) {
                                if(unit != null) {
                                        unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                                }
                        }
                        selectedUnits.Clear();

                        // Selectable units only.
                        RaycastHit hit;
                        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<8)) {
                                var unit = hit.transform.gameObject;
                                unit.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
                                selectedUnits.Add(unit);
                        }
                }

                if(Input.GetButtonUp("Action")) {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        // Shoot for units first.
                        RaycastHit hit;
                        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<8)) {
                                Entity target = hit.transform.gameObject.GetComponent<Entity>();
                                foreach(var unit in selectedUnits) {
                                        if(unit != null) ComSat.IssueAttack(unit.GetComponent<Entity>(), target);
                                }
                        // Otherwise, do a move.
                        } else if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<9)) {
                                DVector2 point = new DVector2((DReal)hit.point.z, (DReal)hit.point.x);
                                foreach(var unit in selectedUnits) {
                                        if(unit != null) ComSat.IssueMove(unit.GetComponent<Entity>(), point);
                                }
                        }
                }

                if(Input.GetMouseButtonDown(0)) {
                        marqueeActive = true;
                        marqueeOrigin = MousePosition();
                }

                if(Input.GetMouseButtonUp(0)) {
                        if(marqueeRect.width != 0 && marqueeRect.height != 0) {
                                foreach(var unit in selectedUnits) {
                                        if(unit != null) {
                                                unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                                        }
                                }
                                selectedUnits.Clear();

                                var selectableUnits = GameObject.FindGameObjectsWithTag("MultiSelectableUnit");
                                foreach(GameObject unit in selectableUnits) {
                                        if(unit.GetComponent<Entity>().team != ComSat.localTeam) continue;

                                        // Convert the world position of the unit to a screen position and then to a GUI point.
                                        Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position);
                                        Vector2 screenPoint = new Vector2(screenPos.x, Screen.height - screenPos.y);

                                        // Ensure that any units not within the marquee are currently unselected.
                                        if(marqueeRect.Contains(screenPoint)) {
                                                unit.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
                                                selectedUnits.Add(unit);
                                        }
                                }
                        }

                        // Reset the marquee so it no longer appears on the screen.
                        marqueeRect.width = 0;
                        marqueeRect.height = 0;
                        marqueeActive = false;
                }

                if(Input.GetMouseButton(0)) {
                        Vector2 mouse = MousePosition();

                        // Compute a new marquee rectangle.
                        marqueeRect.x = marqueeOrigin.x;
                        marqueeRect.y = marqueeOrigin.y;
                        marqueeRect.width = mouse.x - marqueeOrigin.x;
                        marqueeRect.height = mouse.y - marqueeOrigin.y;

                        // Prevent negative widths/heights.
                        if(marqueeRect.width < 0) {
                                marqueeRect.x += marqueeRect.width;
                                marqueeRect.width = -marqueeRect.width;
                        }
                        if(marqueeRect.height < 0) {
                                marqueeRect.y += marqueeRect.height;
                                marqueeRect.height = -marqueeRect.height;
                        }
                }
        }
}
