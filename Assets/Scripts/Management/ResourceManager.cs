using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(Entity))]
public class ResourceManager : MonoBehaviour {
        public ResourceSet[] teamResources;

        void Awake() {
                teamResources = Enumerable.Range(0, 8).Select(_ => new ResourceSet { Metal = 2000, MagicSmoke = 500 }).ToArray();
        }

        public void AddResource(int team, ResourceType resource, int amount) {
                switch(resource) {
                        case ResourceType.MagicSmoke:
                                teamResources[team].MagicSmoke += amount;
                                break;
                        case ResourceType.Metal:
                                teamResources[team].Metal += amount;
                                break;
                }
        }

        public bool TakeResources(int team, ResourceSet resources) {
                var rs = teamResources[team];
                if(rs.ContainsAtLeast(resources)) {
                        rs.Metal -= resources.Metal;
                        rs.MagicSmoke -= resources.MagicSmoke;
                        return true;
                } else {
                        return false;
                }
        }
}
