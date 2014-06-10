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
        public ResourceSet buildCost;
        public bool buildAtPoint;

        public bool sellable;

        public bool hitOnlyIfTargetted = false;

        public Renderer teamColourRenderer;

        private List<System.Action> updateActions = new List<System.Action>();
        private List<System.Action> instantiateActions = new List<System.Action>();
        private List<System.Action<DestroyReason>> destroyActions = new List<System.Action<DestroyReason>>();

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

        public void DestroyAction(DestroyReason reason) {
                foreach(var a in destroyActions) {
                        a(reason);
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

        public void AddDestroyAction(System.Action<DestroyReason> action) {
                ComSat.Trace(this, "AddDestroyAction");
                destroyActions.Add(action);
        }

        public void Damage(int damage) {
                ComSat.Trace(this, "Damage");
                health -= damage;
                if(health <= 0) {
                        ComSat.DestroyEntity(this, DestroyReason.Damaged);
                }
        }

        public void Sell() {
                var value = health / maxHealth * ((DReal)3 / 4);
                var resources = buildCost * value;

                print("Selling " + this + " for " + resources.Metal + " Metal and " + resources.MagicSmoke + " Smoke");
                print("Value: " + value);
                var resourceMan = FindObjectOfType<ResourceManager>();
                resourceMan.teamResources[team] += resources;
                ComSat.DestroyEntity(this, DestroyReason.Sold);
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

        public string BuildToolTip() {
                return gameObject.ToString() + "\nBuild time: " + buildTime + "\nMetal: " + buildCost.Metal + "\nMagic Smoke: " + buildCost.MagicSmoke;
        }
}
