using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(Entity))]
[RequireComponent(typeof(Vehicle))]
public class Constructor : MonoBehaviour {
        Entity entity;
        SimpleMovable movable;

        public Entity[] constructionPrefabs;

        private int buildIndex;
        private DVector2 buildPosition;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                movable = GetComponent<SimpleMovable>();
                buildIndex = -1;
                entity.AddUpdateAction(TickUpdate);
        }

        void UIAction(int what) {
                ComSat.Trace(this, "UIAction");
                if (what < 0 || what >= constructionPrefabs.Length)
                        return;

                var mine = constructionPrefabs[what].GetComponent<Mine>();
                if(mine != null) {
                        var sourceHere = Utility.GetThingAt<ResourceSource>(entity.position);
                        if (sourceHere == null || sourceHere.hasMine || sourceHere.resource != mine.resource)
                                return;
                        
                        buildPosition = sourceHere.GetComponent<Entity>().position;
                        movable.Move(buildPosition);
                        buildIndex = what;
                        return;
                }

                ComSat.SpawnEntity(constructionPrefabs[what].gameObject, entity.team, entity.position, entity.rotation);
                ComSat.DestroyEntity(entity);
        }

        void TickUpdate() {
                if (buildIndex > -1 && (buildPosition - entity.position).sqrMagnitude < 1) {
                        ComSat.SpawnEntity(constructionPrefabs[buildIndex].gameObject, entity.team, buildPosition, entity.rotation);
                        ComSat.DestroyEntity(entity);
                }
        }

        void MoveDestinationChanged() {
                buildIndex = -1;
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

                var sourceHere = Utility.GetThingAt<ResourceSource>(entity.position);

                // TODO: follow Entity.buildTime.
                int x = 10;
                for (int i = 0; i < constructionPrefabs.Length; i++) {
                        var mine = constructionPrefabs[i].GetComponent<Mine>();
                        if(mine != null && (sourceHere == null || sourceHere.hasMine || sourceHere.resource != mine.resource))
                                continue;

                        if(GUI.Button(new Rect(x, Camera.main.pixelHeight - 74, 64, 64), constructionPrefabs[i].buildIcon)) {
                                ComSat.IssueUIAction(entity, i);
                        }
                        x += 74;
                }
        }
}
