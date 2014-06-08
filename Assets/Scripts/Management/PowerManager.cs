using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Entity))]
public class PowerManager : MonoBehaviour {
        public int[] teamPowerSupply = new int[8];
        public int[] teamPowerUse = new int[8];

        Entity entity;
        List<PowerSink> sinks = new List<PowerSink>();
        List<PowerSource> sources = new List<PowerSource>();

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
        }

        void TickUpdate() {
                for(int i = 0; i < teamPowerSupply.Length; i++) {
                        teamPowerSupply[i] = 0;
                        teamPowerUse[i] = 0;
                }

                sinks.RemoveAll(s => !ComSat.EntityExists(s.GetComponent<Entity>()));
                sources.RemoveAll(s => !ComSat.EntityExists(s.GetComponent<Entity>()));

                foreach(var s in sinks) {
                        if(s.poweredOn) {
                                var e = s.GetComponent<Entity>();
                                teamPowerUse[e.team] += s.powerUsage;
                        }
                }
                foreach(var s in sources) {
                        var e = s.GetComponent<Entity>();
                        teamPowerSupply[e.team] += s.currentPower;
                }
        }

        public void AddSink(PowerSink s) {
                sinks.Add(s);
        }

        public void AddSource(PowerSource s) {
                sources.Add(s);
        }

        public bool TeamHasEnoughPower(int team) {
                return teamPowerSupply[team] >= teamPowerUse[team];
        }
}
