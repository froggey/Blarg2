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

        List<GameObject>[] unitGroups = new List<GameObject>[10];

        void OnGUI() {
                if(marqueeActive) {
                        GUI.color = overlayColour;
                        GUI.DrawTexture(marqueeRect, marqueeGraphics);
                }
                for(int i = 0; i < unitGroups.Length; ++i) {
                        if(unitGroups[i] == null) continue;
                        foreach(var unit in unitGroups[i]) {
                                if(unit == null) continue;
                                if(!selectedUnits.Exists(other => unit == other)) continue;
                                Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position);
                                GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 64, 24), i.ToString());
                        }
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

                bool addToSelection = ModifierActive("left shift") || ModifierActive("right shift");

                if(Input.GetButtonUp("Select")) {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                        if(!addToSelection) {
                                foreach(var unit in selectedUnits) {
                                        if(unit != null) {
                                                unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                                        }
                                }
                                selectedUnits.Clear();
                        }

                        // Selectable units only.
                        RaycastHit hit;
                        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<8)) {
                                var unit = hit.transform.gameObject;
                                if(!selectedUnits.Contains(unit)) {
                                        unit.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
                                        selectedUnits.Add(unit);
                                } else if(addToSelection) {
                                        unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                                        selectedUnits.Remove(unit);
                                }
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
                                if(!addToSelection) {
                                        foreach(var unit in selectedUnits) {
                                                if(unit != null) {
                                                        unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                                                }
                                        }
                                        selectedUnits.Clear();
                                }

                                var selectableUnits = GameObject.FindGameObjectsWithTag("MultiSelectableUnit");
                                foreach(GameObject unit in selectableUnits) {
                                        Entity e = unit.GetComponent<Entity>();
                                        if(e == null) {
                                                Debug.LogWarning("No entity in unit " + unit);
                                                continue;
                                        }
                                        if(e.team != ComSat.localTeam) continue;

                                        // Convert the world position of the unit to a screen position and then to a GUI point.
                                        Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position);
                                        Vector2 screenPoint = new Vector2(screenPos.x, Screen.height - screenPos.y);

                                        if(!selectedUnits.Contains(unit) && marqueeRect.Contains(screenPoint)) {
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

                GroupStuff();
        }

        bool ModifierActive(string name) {
                return Input.GetKey(name) || Input.GetKeyUp(name) || Input.GetKeyDown(name);
        }

        void GroupStuff() {
                int groupID = -1;
                if(Input.GetButtonUp("Group 0")) {
                        groupID = 0;
                } else if(Input.GetButtonUp("Group 1")) {
                        groupID = 1;
                } else if(Input.GetButtonUp("Group 2")) {
                        groupID = 2;
                } else if(Input.GetButtonUp("Group 3")) {
                        groupID = 3;
                } else if(Input.GetButtonUp("Group 4")) {
                        groupID = 4;
                } else if(Input.GetButtonUp("Group 5")) {
                        groupID = 5;
                } else if(Input.GetButtonUp("Group 6")) {
                        groupID = 6;
                } else if(Input.GetButtonUp("Group 7")) {
                        groupID = 7;
                } else if(Input.GetButtonUp("Group 8")) {
                        groupID = 8;
                } else if(Input.GetButtonUp("Group 9")) {
                        groupID = 9;
                } else {
                        return;
                }

                if(ModifierActive("left ctrl") || ModifierActive("right ctrl")) {
                        // Set group.
                        unitGroups[groupID] = new List<GameObject>(selectedUnits);
                } else if(ModifierActive("left shift") || ModifierActive("right shift")) {
                        // Merge group with current.
                        foreach(var unit in unitGroups[groupID]) {
                                if(unit == null) continue;
                                if(selectedUnits.Contains(unit)) continue;
                                unit.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
                                selectedUnits.Add(unit);
                        }
                } else {
                        // Load group.
                        foreach(var unit in selectedUnits) {
                                if(unit != null) {
                                        unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                                }
                        }
                        if(unitGroups[groupID] == null) {
                                selectedUnits.Clear();
                        } else {
                                selectedUnits = new List<GameObject>(unitGroups[groupID]);
                                foreach(var unit in selectedUnits) {
                                        if(unit != null) {
                                                unit.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
                                        }
                                }
                        }
                }

                // Flush away any null units in the groups.
                foreach(var group in unitGroups) {
                        if(group == null) continue;
                        group.RemoveAll(go => go == null);
                }
        }
}
