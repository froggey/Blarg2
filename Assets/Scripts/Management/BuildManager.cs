using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(Entity))]
public class BuildManager : MonoBehaviour {
        class PendingBuild {
                public PendingBuild(Entity prefab, DVector2 position, int team) {
                        this.prefab = prefab;
                        this.position = position;
                        this.team = team;

                        var prefabSize = (DReal)prefab.collisionRadiusNumerator / prefab.collisionRadiusDenominator;
                        this.sqrRadius = prefabSize * prefabSize;
                        this.ghost = GameObject.Instantiate(prefab.ghostPrefab,
                                                            new Vector3((float)position.y, 0, (float)position.x),
                                                            Quaternion.AngleAxis(0, Vector3.up))
                                as GameObject;
                        if(team != ComSat.localTeam) {
                                this.ghost.SetActive(false);
                        }
                }

                public Entity prefab;
                public DVector2 position;
                public int team;
                public DReal sqrRadius;
                public GameObject ghost;
        }

        List<PendingBuild> pendingBuilds = new List<PendingBuild>();

        public bool CanBuildAt(DVector2 position, DReal radius) {
                foreach(var b in pendingBuilds) {
                        var dist = (position - b.position).sqrMagnitude;
                        if(dist < radius * radius + b.sqrRadius) {
                                return false;
                        }
                }
                return true;
        }

        public GameObject AddPendingBuild(Entity prefab, DVector2 position, int team) {
                var build = new PendingBuild(prefab, position, team);
                pendingBuilds.Add(build);
                return build.ghost;
        }

        public void RemovePendingBuild(GameObject thing) {
                pendingBuilds.RemoveAll(b => b.ghost == thing);
                Destroy(thing);
        }
}
