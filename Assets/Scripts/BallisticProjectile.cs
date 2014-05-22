using UnityEngine;
using System.Collections;

public class BallisticProjectile : Projectile {
        public int damage;
        public int initialSpeed;
        private DVector2 velocity;

        public GameObject impactPrefab;
        public TrailRenderer trail;

        void Start() {
                velocity = DVector2.FromAngle(this.rotation) * initialSpeed;
        }

        public override void TickUpdate() {
                base.TickUpdate();

                DVector2 newPosition = this.position + velocity * ComSat.tickRate;

                DVector2 hitPosition;
                Entity hit = ComSat.LineCast(this.position, newPosition, out hitPosition, this.team);
                if(hit != null) {
                        print("Projectile at " + this.position + " impacted " + hit + " at " + hitPosition);

                        hit.Damage(damage);

                        Vector3 position = new Vector3((float)hitPosition.y, 0, (float)hitPosition.x);
                        Object.Instantiate(impactPrefab, position, Quaternion.AngleAxis((float)this.rotation, Vector3.up));

                        if(trail) {
                                trail.transform.parent = null;
                                trail.autodestruct = true;
                                trail = null;
                        }

                        ComSat.DestroyProjectile(this);
                        return;
                }

                this.position = newPosition;
        }
}
