using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public struct BuildCommandData {
        public int what;
        public DVector2 position;
        public GameObject buildCollider;

        public BuildCommandData(int what, DVector2 position) {
                this.what = what;
                this.position = position;
                this.buildCollider = null;
        }
}

[RequireComponent(typeof(Entity), typeof(PowerSink))]
public class Factory : MonoBehaviour {
        public Entity[] prefabs;

        // Sabotage time remaining.
        DReal sabotageTime;
        // Sabotage lasts this long.
        public int sabotageRepairTime;
        // Sabotage causes everything to take this much longer.
        public int sabotageTimeMultiplier = 3;

        public int buildRadius;

        public bool dontManagePower;

        // Construction time remaining.
        private DReal delay;
        private DReal partialMetalUnit, partialSmokeUnit;
        private ResourceSet usedResources;

        private Queue<BuildCommandData> buildQueue;

        private Entity entity;
        private PowerSink powerSink;

        private const int clearQueue = -1;

        private ResourceManager resourceMan;
        private BuildManager buildMan;
        private PlayerInterface playerInterface;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                entity.AddDestroyAction(DestroyAction);
                powerSink = GetComponent<PowerSink>();
                buildQueue = new Queue<BuildCommandData>();
                resourceMan = FindObjectOfType<ResourceManager>();
                buildMan = FindObjectOfType<BuildManager>();
                playerInterface = FindObjectOfType<PlayerInterface>();
                ResetBuildTime();
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(sabotageTime > 0) {
                        sabotageTime -= ComSat.tickRate;
                }
                if (buildQueue.Any()) {
                        var buildMe = buildQueue.Peek();
                        var prefab = prefabs[buildMe.what];

                        if(delay > 0) {
                                var advance = ComSat.tickRate;
                                if(sabotageTime > 0) {
                                        advance /= sabotageTimeMultiplier;
                                }
                                if(!powerSink.Powered()) {
                                        advance /= 2;
                                }

                                var completion = advance / prefab.buildTime;
                                var totalRemaining = prefab.buildCost - usedResources;
                                partialMetalUnit += DReal.Min(completion * prefab.buildCost.Metal, totalRemaining.Metal);
                                partialSmokeUnit += DReal.Min(completion * prefab.buildCost.MagicSmoke, totalRemaining.MagicSmoke);
                                var rs = new ResourceSet { Metal = (int)partialMetalUnit, MagicSmoke = (int)partialSmokeUnit };
                                if (resourceMan.TakeResources(entity.team, rs)) {
                                        usedResources += rs;
                                        partialMetalUnit %= 1;
                                        partialSmokeUnit %= 1;
                                        delay -= advance;
                                } else {
                                        partialMetalUnit -= completion * prefab.buildCost.Metal;
                                        partialSmokeUnit -= completion * prefab.buildCost.MagicSmoke;
                                }
                        }
                        if(delay <= 0) {
                                if (!resourceMan.TakeResources(entity.team, prefab.buildCost - usedResources)) return;

                                // Timer expired and we're building something.
                                print("Build new " + prefab);
                                var prefabSize = (DReal)prefab.collisionRadiusNumerator / prefab.collisionRadiusDenominator;
                                var wiggle = ((ComSat.RandomValue() % 5) / 5) * ((ComSat.RandomValue() % 2 == 0) ? 1 : -1);
                                var position = prefab.buildAtPoint
                                        ? buildMe.position
                                        : (entity.position + DVector2.FromAngle(entity.rotation + wiggle) * (entity.collisionRadius + prefabSize + 2 + wiggle));
                                ComSat.SpawnEntity(entity, prefab.gameObject, position, 0);
                                if(buildMe.buildCollider != null) {
                                        buildMan.RemovePendingBuild(buildMe.buildCollider);
                                }
                                buildQueue.Dequeue();
                                ResetBuildTime();
                        }
                }
        }

        void DestroyAction() {
                UIAction(clearQueue);
        }

        private void ResetBuildTime() {
                if (buildQueue.Any()) {
                        delay = prefabs[buildQueue.Peek().what].buildTime;
                        if (!dontManagePower) powerSink.poweredOn = true;
                } else {
                        delay = 0;
                        if (!dontManagePower) powerSink.poweredOn = false;
                }
                partialMetalUnit = partialSmokeUnit = 0;
                usedResources = new ResourceSet();
        }

        bool CanPlaceAt(Entity thing, DVector2 position) {
                var dist = (position - entity.position).sqrMagnitude;
                var prefabSize = (DReal)thing.collisionRadiusNumerator / thing.collisionRadiusDenominator;
                var minDist = (prefabSize + entity.collisionRadius) * (prefabSize + entity.collisionRadius);
                var maxDist = buildRadius * buildRadius;

                return minDist < dist && dist < maxDist &&
                        ComSat.FindEntityWithinRadius(position, prefabSize) == null &&
                        buildMan.CanBuildAt(position, prefabSize);
        }

        void UIAction(int what) {
                ComSat.Trace(this, "UIAction");
                if(what == clearQueue) {
                        foreach(var data in buildQueue) {
                                if(data.buildCollider != null) {
                                        buildMan.RemovePendingBuild(data.buildCollider);
                                }
                        }
                        buildQueue.Clear();
                        delay = 0;
                        resourceMan.AddResource(entity.team, ResourceType.Metal, usedResources.Metal);
                        resourceMan.AddResource(entity.team, ResourceType.MagicSmoke, usedResources.MagicSmoke);
                        ResetBuildTime();
                }
        }

        void Build(BuildCommandData data) {
                if(prefabs[data.what].buildAtPoint) {
                        if(!CanPlaceAt(prefabs[data.what], data.position)) {
                                return;
                        }
                        data.buildCollider = buildMan.AddPendingBuild(prefabs[data.what], data.position, entity.team);
                }
                buildQueue.Enqueue(data);
                if (buildQueue.Count == 1) {
                        ResetBuildTime();
                }
        }

        public void Sabotage() {
                ComSat.Trace(this, "Sabotage");
                sabotageTime += sabotageRepairTime;
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
                        if(sabotageTime > 0) {
                                GUI.Box(new Rect(10, Camera.main.pixelHeight - 100, 64, 25), ((int)Mathf.Ceil((float)(delay * sabotageTimeMultiplier))).ToString());
                        } else {
                                GUI.Box(new Rect(10, Camera.main.pixelHeight - 100, 64, 25), ((int)Mathf.Ceil((float)delay)).ToString());
                        }
                } else {
                        GUI.Box(new Rect(10, Camera.main.pixelHeight - 100, 64, 25), "Ready");
                }
                if(sabotageTime > 0) {
                        GUI.Box(new Rect(84, Camera.main.pixelHeight - 100, 100, 25), "Sabotaged! " + Mathf.Ceil((float)sabotageTime));
                }

                var building = buildQueue.Any() ? buildQueue.Peek().what : -1;

                for(int i = 0; i < prefabs.Length; ++i) {
                        GUI.backgroundColor = (i == building && Time.time % 1 < 0.5f) ? Color.green : Color.white;
                        // Closures!
                        var prefab = prefabs[i];
                        var id = i;

                        if(GUI.Button(new Rect(10 + i * 74, Camera.main.pixelHeight - 74, 64, 64),
                                      new GUIContent(prefab.buildIcon, prefab.BuildToolTip()))) {
                                if(prefab.buildAtPoint) {
                                        playerInterface.PlaceThingOnTerrain(prefab.ghostPrefab,
                                                                            position => { return CanPlaceAt(prefab, position); },
                                                                            position => ComSat.IssueBuild(entity, id, position));
                                } else {
                                        ComSat.IssueBuild(entity, id, new DVector2());
                                }
                        }

                        GUI.backgroundColor = Color.white;

                        var queued = buildQueue.Count(qi => qi.what == i);
                        if(queued > 0) {
                                GUI.Label(new Rect(14 + i * 74, Camera.main.pixelHeight - 70, 64, 24), queued.ToString());
                        }
                }

                GUI.Label(new Rect(0,30,200,300), GUI.tooltip);

                if(buildQueue.Any() && GUI.Button(new Rect(10 + prefabs.Length * 74, Camera.main.pixelHeight - 74, 64, 64), "Stop")) {
                        ComSat.IssueUIAction(entity, clearQueue);
                }
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, buildRadius);
        }
}
