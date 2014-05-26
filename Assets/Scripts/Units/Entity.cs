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

        private bool isSelected;

        // This is super dumb.
        // The inspector can't display DReals, so expose the collisionRadius as a fraction.
        public int collisionRadiusNumerator;
        public int collisionRadiusDenominator = 1;

        public DReal collisionRadius;

        public GameObject baseMesh;

        public int buildTime = 0;
        public Texture2D buildIcon;
        public Texture2D hpBarTexture;

        public bool hitOnlyIfTargetted = false;

        public Renderer teamColourRenderer;

        private Dictionary<int, System.Action> updateActions = new Dictionary<int, System.Action>();

        void Awake() {
                collisionRadius = (DReal)collisionRadiusNumerator / collisionRadiusDenominator;

                health = maxHealth;
        }

        void Start() {
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
                foreach(var a in updateActions.Values) {
                        a();
                }
                position += velocity * ComSat.tickRate;
        }

        public void AddUpdateAction(int priority, System.Action action) {
                if(updateActions.ContainsKey(priority)) {
                        Debug.LogError("Action with conflicting priority " + priority);
                        AddUpdateAction(priority+1, action);
                        return;
                }

                updateActions[priority] = action;
        }

        public void Damage(int damage) {
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
