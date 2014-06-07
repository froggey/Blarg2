using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Entity : MonoBehaviour {
        public DVector2 position;
        public DReal rotation;
        public DVector2 velocity;

        public int team;
        public Entity origin; // who spawned this.

        public int maxHealth;
        public int health;

        public bool isSelected { get; private set; }

        // This is super dumb.
        // The inspector can't display DReals, so expose the collisionRadius as a fraction.
        public int collisionRadiusNumerator;
        public int collisionRadiusDenominator = 1;

        public DReal collisionRadius;

        public GameObject baseMesh;

        // Ghost is used during construction and formation moves.
        public GameObject ghostPrefab;

        public int buildTime = 0;
        public Texture2D buildIcon;
        public Texture2D hpBarTexture;

        public bool hitOnlyIfTargetted = false;

        public Renderer teamColourRenderer;

        public ResourceSet buildCost;

        private List<System.Action> updateActions = new List<System.Action>();
        private List<System.Action> instantiateActions = new List<System.Action>();

        void Awake() {
                ComSat.Trace(this, "Awake");
                collisionRadius = (DReal)collisionRadiusNumerator / collisionRadiusDenominator;
        }

        public void OnInstantiate() {
                health = maxHealth;
                velocity = new DVector2();
                isSelected = false;
                OnUnselected();
                foreach(var a in instantiateActions) {
                        a();
                }
        }

        void Start() {
                ComSat.Trace(this, "Start");
                if(baseMesh && team != 0) {
                        baseMesh.renderer.material.color = Utility.TeamColour(team);
                }
        }

        void Update() {
                if(teamColourRenderer != null) {
                        teamColourRenderer.material.SetColor("_TeamColor", Utility.TeamColour(team));
                }

                transform.localPosition = new Vector3((float)position.y,
                                                      transform.localPosition.y,
                                                      (float)position.x);
                transform.localRotation = Quaternion.AngleAxis((float)DReal.Degrees(rotation), Vector3.up);
        }

        public void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                foreach(var a in updateActions) {
                        a();
                }
                position += velocity * ComSat.tickRate;
        }

        public void AddUpdateAction(System.Action action) {
                ComSat.Trace(this, "AddUpdateAction");
                updateActions.Add(action);
        }

        public void AddInstantiateAction(System.Action action) {
                ComSat.Trace(this, "AddInstantiateAction");
                instantiateActions.Add(action);
        }

        public void Damage(int damage) {
                ComSat.Trace(this, "Damage");
                health -= damage;
                if(health <= 0) {
                        ComSat.DestroyEntity(this);
                }
        }

        void OnDrawGizmosSelected() {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, (float)collisionRadiusNumerator / collisionRadiusDenominator);
        }

        private void OnSelected() {
                isSelected = true;
        }

        private void OnUnselected() {
                isSelected = false;
        }

        void OnGUI() {
                if (!isSelected) return;

                var p = Camera.main.WorldToScreenPoint(transform.position);
                GUI.DrawTextureWithTexCoords(
                        new Rect(p.x - 16, Camera.main.pixelHeight - p.y + 8, 32, 4),
                        hpBarTexture,
                        new Rect((float)(maxHealth - health) / maxHealth / 2, 0, 0.5f, 1));
        }
}
