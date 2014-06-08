using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Entity))]
public class PowerManager : MonoBehaviour {
        public int[] teamPowerSupply = new int[8];
        public int[] teamPowerUse = new int[8];

        struct Pair<T> {
                public Pair(Entity e, T thing) {
                        this.e = e;
                        this.thing = thing;
                }

                public Entity e;
                public T thing;
        }

        Entity entity;
        List<Pair<PowerSink>> sinks = new List<Pair<PowerSink>>();
        List<Pair<PowerSource>> sources = new List<Pair<PowerSource>>();

        void Awake() {
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
        }

        void TickUpdate() {
                for(int i = 0; i < teamPowerSupply.Length; i++) {
                        teamPowerSupply[i] = 0;
                        teamPowerUse[i] = 0;
                }

                sinks.RemoveAll(s => !ComSat.EntityExists(s.e));
                sources.RemoveAll(s => !ComSat.EntityExists(s.e));

                foreach(var s in sinks) {
                        if(s.thing.poweredOn) {
                                teamPowerUse[s.e.team] += s.thing.powerUsage;
                        }
                }
                foreach(var s in sources) {
                        teamPowerSupply[s.e.team] += s.thing.currentPower;
                }
        }

        public void AddSink(PowerSink s) {
                sinks.Add(new Pair<PowerSink>(s.GetComponent<Entity>(), s));
        }

        public void AddSource(PowerSource s) {
                sources.Add(new Pair<PowerSource>(s.GetComponent<Entity>(), s));
        }

        public bool TeamHasEnoughPower(int team) {
                return teamPowerSupply[team] >= teamPowerUse[team];
        }
}
