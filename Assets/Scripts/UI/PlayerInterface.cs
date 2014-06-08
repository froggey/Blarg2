using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerInterface : MonoBehaviour {
        public int selectableThingLayer = 8;
        public int terrainLayer = 9;
        private bool disabledBecauseGUI = false;

        public Texture marqueeGraphics;
        public Color overlayColour;
        Vector2 marqueeOrigin;
        Rect marqueeRect;
        bool marqueeActive;
        enum MarqueeMode { Select, Target };
        MarqueeMode marqueeMode;

        List<GameObject> selectedUnits = new List<GameObject>();

        List<GameObject>[] unitGroups = new List<GameObject>[10];

        PowerManager powerMan;
        ResourceManager resourceMan;

        // Thing placement.
        GameObject activeGhost;
        System.Func<DVector2, bool> placementValidCallback;
        System.Action<DVector2> placeCallback;

        void Update() {
                if(powerMan == null) {
                        powerMan = FindObjectOfType<PowerManager>();
                }
                if(resourceMan == null) {
                        resourceMan = FindObjectOfType<ResourceManager>();
                }
        }

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

                if(ComSat.localTeam != -1) {
                        GUI.color = Color.white;
                        if(resourceMan) {
                                GUI.Label(new Rect(0, 0, 128, 24), "Metal: " + resourceMan.teamResources[ComSat.localTeam].Metal);
                                GUI.Label(new Rect(128, 0, 128, 24), "Magic Smoke: " + resourceMan.teamResources[ComSat.localTeam].MagicSmoke);
                        }
                        if(powerMan != null) {
                                var powerUse = powerMan.teamPowerUse[ComSat.localTeam];
                                var powerSupply = powerMan.teamPowerSupply[ComSat.localTeam];
                                if (powerUse > powerSupply) GUI.color = Color.red;
                                GUI.Label(new Rect(256, 0, 256, 24), "Power Usage: " + powerUse + "/" + powerSupply);
                                if (powerUse > powerSupply) GUI.color = Color.white;
                        }
                }
        }

        public void PlaceThingOnTerrain(GameObject ghostPrefab, System.Func<DVector2, bool> placementValidCallback, System.Action<DVector2> placeCallback) {
                if(activeGhost != null) {
                        Destroy(activeGhost);
                }
                marqueeRect.width = 0;
                marqueeRect.height = 0;
                marqueeActive = false;

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                Vector3 position = new Vector3();
                if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<terrainLayer)) {
                        position = hit.point;
                }

                activeGhost = GameObject.Instantiate(ghostPrefab, position, Quaternion.AngleAxis(0, Vector3.up)) as GameObject;
                this.placementValidCallback = placementValidCallback;
                this.placeCallback = placeCallback;
        }

        Vector2 MousePosition() {
                // Must invert y. GUI & input y coordinates disagree.
                return new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        }

        void ClearSelected() {
                foreach(var unit in selectedUnits) {
                        if(unit != null) {
                                unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                        }
                }
                selectedUnits.Clear();
        }

        void SelectUnit(GameObject unit) {
                if(selectedUnits.Contains(unit)) return;
                unit.SendMessage("OnSelected", SendMessageOptions.DontRequireReceiver);
                selectedUnits.Add(unit);
        }

        void DeselectUnit(GameObject unit) {
                unit.SendMessage("OnUnselected", SendMessageOptions.DontRequireReceiver);
                selectedUnits.Remove(unit);
        }

        void TryToPlaceThing() {
                if(Input.GetMouseButtonUp(0)) {
                        var position = new DVector2((DReal)activeGhost.transform.position.z, (DReal)activeGhost.transform.position.x);
                        if(placementValidCallback(position)) {
                                placeCallback(position);
                                Destroy(activeGhost);
                                placementValidCallback = null;
                                placeCallback = null;
                                return;
                        }
                } else if(Input.GetMouseButtonUp(1)) {
                        Destroy(activeGhost);
                        placementValidCallback = null;
                        placeCallback = null;
                        return;
                }

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<terrainLayer)) {
                        activeGhost.transform.position = hit.point;
                        var position = new DVector2((DReal)activeGhost.transform.position.z, (DReal)activeGhost.transform.position.x);
                        var colour = placementValidCallback(position) ? Color.green : Color.red;
                        foreach(var r in activeGhost.GetComponentsInChildren<Renderer>()) {
                                r.material.color = colour;
                        }
                }
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

                if(activeGhost != null) {
                        TryToPlaceThing();
                        return;
                }

                bool addToSelection = ModifierActive("left shift") || ModifierActive("right shift");

                if(Input.GetButtonUp("Select")) {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                        if(!addToSelection) {
                                ClearSelected();
                        }

                        // Selectable units only.
                        RaycastHit hit;
                        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<selectableThingLayer)) {
                                var unit = hit.transform.gameObject;
                                if(!selectedUnits.Contains(unit)) {
                                        SelectUnit(unit);
                                } else if(addToSelection) {
                                        DeselectUnit(unit);
                                }
                        }
                }

                if(Input.GetButtonUp("Action")) {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        // Shoot for units first.
                        RaycastHit hit;
                        if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<selectableThingLayer)) {
                                Entity target = hit.transform.gameObject.GetComponent<Entity>();
                                foreach(var unit in selectedUnits) {
                                        if(unit == null) continue;
                                        ComSat.IssueAttack(unit.GetComponent<Entity>(), new[] { target });
                                }
                        // Otherwise, do a move.
                        } else if(Physics.Raycast(ray, out hit, Mathf.Infinity, 1<<terrainLayer)) {
                                DVector2 point = new DVector2((DReal)hit.point.z, (DReal)hit.point.x);
                                foreach(var unit in selectedUnits) {
                                        if(unit == null) continue;
                                        ComSat.IssueMove(unit.GetComponent<Entity>(), point);
                                }
                        }
                }

                if(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
                        marqueeMode = Input.GetMouseButtonDown(0) ? MarqueeMode.Select : MarqueeMode.Target;
                        marqueeActive = true;
                        marqueeOrigin = MousePosition();
                }

                if(Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) {
                        if(marqueeRect.width != 0 && marqueeRect.height != 0) {
                                var selection = new List<Entity>();

                                if(!addToSelection && Input.GetMouseButtonUp(0)) {
                                        ClearSelected();
                                }

                                var selectableUnits = GameObject.FindGameObjectsWithTag("MultiSelectableUnit");
                                foreach(GameObject unit in selectableUnits) {
                                        Entity e = unit.GetComponent<Entity>();
                                        if(e == null) {
                                                Debug.LogWarning("No entity in unit " + unit);
                                                continue;
                                        }
                                        if(marqueeMode == MarqueeMode.Select && e.team != ComSat.localTeam) continue;
                                        if(marqueeMode == MarqueeMode.Target && e.team == ComSat.localTeam) continue;

                                        // Convert the world position of the unit to a screen position and then to a GUI point.
                                        Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position);
                                        Vector2 screenPoint = new Vector2(screenPos.x, Screen.height - screenPos.y);

                                        if(marqueeRect.Contains(screenPoint)) {
                                                selection.Add(e);
                                        }
                                }

                                if (marqueeMode == MarqueeMode.Select) {
                                        foreach (var u in selection) {
                                                SelectUnit(u.gameObject);
                                        }
                                }
                                else {
                                        var a = selection.ToArray();
                                        foreach (var attacker in selectedUnits) {
                                                if (attacker == null) continue;
                                                ComSat.IssueAttack(attacker.GetComponent<Entity>(), a);
                                        }
                                }
                        }

                        // Reset the marquee so it no longer appears on the screen.
                        marqueeRect.width = 0;
                        marqueeRect.height = 0;
                        marqueeActive = false;
                }

                if(Input.GetMouseButton(0) || Input.GetMouseButton(1)) {
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
                                SelectUnit(unit);
                        }
                } else {
                        // Load group.
                        ClearSelected();
                        if(unitGroups[groupID] != null) {
                                foreach(var unit in unitGroups[groupID]) {
                                        SelectUnit(unit);
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
