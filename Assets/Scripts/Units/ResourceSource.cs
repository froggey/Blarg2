using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity))]
public class ResourceSource : MonoBehaviour {
        public ResourceType resource;
        public int amount;
        public bool hasMine;

        private Entity entity;
        private ParticleSystem particles;

        void Awake() {
                entity = GetComponent<Entity>();
                particles = GetComponentInChildren<ParticleSystem>();
        }

        void Update() {
                if (particles != null)
                        particles.enableEmission = !hasMine && amount > 0;
        }
}
