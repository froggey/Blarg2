using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

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

        // Construction time remaining.
        private DReal delay;
        private DReal partialMetalUnit, partialSmokeUnit;
        private ResourceSet usedResources;

        private Queue<int> buildQueue;

        private Entity entity;
        private PowerSink powerSink;

        private const int clearQueue = -1;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                powerSink = GetComponent<PowerSink>();
                buildQueue = new Queue<int>();
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(sabotageTime > 0) {
                        sabotageTime -= ComSat.tickRate;
                }
                if (buildQueue.Any()) {
                        var buildMe = buildQueue.Peek();

                        if(delay > 0) {
                                var advance = ComSat.tickRate;
                                if(sabotageTime > 0) {
                                        advance /= sabotageTimeMultiplier;
                                }
                                if(!ComSat.TeamHasEnoughPower(entity.team)) {
                                        advance /= 2;
                                }
                                advance *= 10;

                                var completion = advance / prefabs[buildMe].buildTime;
                                partialMetalUnit += completion * prefabs[buildMe].buildCost.Metal;
                                partialSmokeUnit += completion * prefabs[buildMe].buildCost.MagicSmoke;
                                var rs = new ResourceSet { Metal = (int)partialMetalUnit, MagicSmoke = (int)partialSmokeUnit };
                                if (ComSat.TakeResources(entity.team, rs)) {
                                        usedResources += rs;
                                        partialMetalUnit %= 1;
                                        partialSmokeUnit %= 1;
                                        delay -= advance;
                                } else {
                                        partialMetalUnit -= completion * prefabs[buildMe].buildCost.Metal;
                                        partialSmokeUnit -= completion * prefabs[buildMe].buildCost.MagicSmoke;
                                }
                        }
                        if(delay <= 0) {
                                Debug.Log(prefabs[buildMe].buildCost - usedResources);
                                if (!ComSat.TakeResources(entity.team, prefabs[buildMe].buildCost - usedResources)) return;

                                // Timer expired and we're building something.
                                print("Build new " + prefabs[buildMe]);
                                var rotation = ComSat.RandomRange(0, DReal.TwoPI);
                                var prefabSize = (DReal)prefabs[buildMe].collisionRadiusNumerator / prefabs[buildMe].collisionRadiusDenominator; // Blech. Sorry :(
                                var offset = DVector2.FromAngle(rotation) * (ComSat.RandomRange(0, buildRadius) + entity.collisionRadius * 2 + prefabSize);
                                ComSat.SpawnEntity(entity, prefabs[buildMe].gameObject, entity.position + offset, rotation);

                                buildQueue.Dequeue();
                                ResetBuildTime();
                        }
                }
        }

        private void ResetBuildTime() {
                if (buildQueue.Any()) {
                        delay = prefabs[buildQueue.Peek()].buildTime;
                        powerSink.poweredOn = true;
                }
                else {
                        delay = 0;
                        powerSink.poweredOn = false;
                }
                partialMetalUnit = partialSmokeUnit = 0;
                usedResources = new ResourceSet();
        }

        void UIAction(int what) {
                ComSat.Trace(this, "UIAction");
                if(what == clearQueue) {
                        buildQueue.Clear();
                        delay = 0;
                        ComSat.AddResource(entity.team, ResourceType.Metal, usedResources.Metal);
                        ComSat.AddResource(entity.team, ResourceType.MagicSmoke, usedResources.MagicSmoke);
                        ResetBuildTime();
                }
                else if(what >= 0 && what < prefabs.Length) {
                        buildQueue.Enqueue(what);
                        if (buildQueue.Count == 1) {
                                ResetBuildTime();
                        }
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

                var building = buildQueue.Any() ? buildQueue.Peek() : -1;

                for(int i = 0; i < prefabs.Length; ++i) {
                        GUI.backgroundColor = (i == building && Time.time % 1 < 0.5f) ? Color.green : Color.white;

                        if(GUI.Button(new Rect(10 + i * 74, Camera.main.pixelHeight - 74, 64, 64),
                                      new GUIContent(prefabs[i].buildIcon, prefabs[i].ToString() + "\nBuild time: " + prefabs[i].buildTime + "\nMetal: " + prefabs[i].buildCost.Metal + "\nMagic Smoke: " + prefabs[i].buildCost.MagicSmoke))) {
                                ComSat.IssueUIAction(entity, i);
                        }

                        GUI.backgroundColor = Color.white;

                        var queued = buildQueue.Count(qi => qi == i);
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
