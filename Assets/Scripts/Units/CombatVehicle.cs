using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity), typeof(Vehicle))]
public class CombatVehicle : MonoBehaviour {
        public GameObject projectilePrefab;

        public int attackRange;
        // Will chase an enemy this far in IDLE.
        public int tetherRange;
        // Turret moves this fast. Degrees/second.
        public int turretTurnSpeed;

        // Turret returns to 0 when not attacking.
        public bool turretAutoResets;

        static readonly DReal sqrPositioningAccuracy = (DReal)1 / 100;

        public enum Mode {
                IDLE, MOVE, ATTACK
        }

        public enum Stance {
                AGGRESSIVE, // Chase enemies that get within range, without limit.
                GUARD, // Chase enemies that get within range, limited by tetherRange.
                HOLD_GROUND, // Fire at enemies within range, without moving.
                HOLD_FIRE, // Don't shoot anything.
        }

        public Stance stance = Stance.GUARD;

        [HideInInspector]
        public DReal turretRotation;

        [HideInInspector]
        public Mode mode;
        [HideInInspector]
        public DVector2 destination; // Current movement target.
        [HideInInspector]
        public Entity target; // Current attack target.

        private Entity[] targets;
        private bool movingToPoint;

        private Entity entity;
        private Vehicle vehicle;
        private UnitSelection unitSelection;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                entity.AddInstantiateAction(InstantiateAction);
                vehicle = GetComponent<Vehicle>();
                unitSelection = GetComponent<UnitSelection>();
        }

        void InstantiateAction() {
                mode = Mode.IDLE;
                destination = entity.position;
        }

        void Attack(Entity[] targets) {
                ComSat.Trace(this, "Attack");
                mode = Mode.ATTACK;
                target = null;
                this.targets = targets;
                vehicle.Stop();
        }

        void Move(DVector2 location) {
                ComSat.Trace(this, "Move");
                mode = Mode.MOVE;
                if(stance == Stance.HOLD_FIRE) {
                        target = null;
                }
                targets = null;
                destination = location;
        }

        private void PickNewTarget() {
                if (targets == null) targets = new Entity[] {};

                if (targets.Count() > 0) {
                        target = targets[0];
                        mode = Mode.ATTACK;
                } else {
                        target = null;
                        mode = Mode.IDLE;
                }
        }

        // Possibly compute a new target to attack.
        void UpdateTarget() {
                if(ComSat.EntityExists(target)) {
                        return;
                }

                if(mode == Mode.ATTACK) {
                        // Pick the closest target given by the user.
                        // FIXME: Groups of units should focus-fire.
                        targets = targets.Where(t => ComSat.EntityExists(t)).OrderBy(t => (t.position - entity.position).sqrMagnitude).ToArray();
                        if(targets.Count() > 0) {
                                // Have a user-provided target, stay in attack mode.
                                target = targets[0];
                                return;
                        } else {
                                // Nothing else to attack. Sit here.
                                mode = Mode.IDLE;
                                destination = entity.position;
                        }
                }
                if(stance != Stance.HOLD_FIRE) {
                        // Find something to bother.
                        target = ComSat.FindEntityWithinRadius(entity.position, attackRange, entity.team);
                }
        }

        // Test if the target is still notable at the given distance.
        bool CareAboutTarget(DReal dist) {
                if(mode == Mode.ATTACK) {
                        // Always care when attacking.
                        return true;
                }
                if(mode == Mode.MOVE) {
                        // Only care if in range.
                        return dist < attackRange;
                }
                if(stance == Stance.AGGRESSIVE) {
                        // Always care when aggressive.
                        return true;
                }
                if(stance == Stance.GUARD) {
                        // Stay in tether range when guarding.
                        return (destination - entity.position).magnitude < tetherRange;
                }
                if(stance == Stance.HOLD_GROUND) {
                        // Don't chase, only care if in range.
                        return dist < attackRange;
                }
                return false;
        }

        // Try to attack the target, may forget about the target.
        // Returns true if the vehicle should move closer.
        bool MaybeAttackTarget() {
                if(target == null) {
                        return false;
                }

                var distVec = target.position - entity.position;
                var dist = distVec.magnitude;

                // Do should we still care about this target?

                if(!CareAboutTarget(dist)) {
                        target = null;
                        return false;
                }

                if(dist > attackRange) {
                        // Out of range, chase the fucker down.
                        return true;
                }

                // Point turret.
                DReal targetTurretAngle;

                var projectileProjectile = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
                if(projectileProjectile != null) {
                        var aimSpot = Utility.PredictShot(entity.position, projectileProjectile.initialSpeed,
                                                          target.position, target.velocity);

                        targetTurretAngle = DReal.Mod(DVector2.ToAngle(aimSpot - entity.position) - entity.rotation, DReal.TwoPI);
                } else {
                        targetTurretAngle = DReal.Mod(DVector2.ToAngle(distVec) - entity.rotation, DReal.TwoPI);
                }

                if(dist >= attackRange) {
                        targetTurretAngle = turretAutoResets ? 0 : turretRotation;
                }
                turretRotation = Utility.CalculateNewAngle(turretRotation, targetTurretAngle, turretTurnSpeed);
                SendMessage("TurnTurret", turretRotation);

                // Fire when in range and pointing the gun at the target.
                if(dist < attackRange && targetTurretAngle == turretRotation) {
                        SendMessage("Fire");
                }

                return false;
        }

        // Move towards a position, returning true when we've reached it.
        bool MoveTowards(DVector2 loc) {
                if((loc - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                        // Close enough.
                        vehicle.Stop();
                        return true;
                } else {
                        vehicle.MoveTowards(loc);
                        return false;
                }
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                UpdateTarget();
                var shouldMoveToTarget = MaybeAttackTarget();

                if(mode == Mode.ATTACK && !shouldMoveToTarget) {
                        vehicle.Stop();
                } else if(mode == Mode.MOVE || (mode == Mode.IDLE && stance == Stance.HOLD_GROUND) || !shouldMoveToTarget) {
                        // Drop into idle after move destination has been reached.
                        if(MoveTowards(destination) && mode == Mode.MOVE) {
                                mode = Mode.IDLE;
                        }
                } else {
                        MoveTowards(target.position);
                }

                if(target == null && turretAutoResets) {
                        turretRotation = Utility.CalculateNewAngle(turretRotation, 0, turretTurnSpeed);
                        SendMessage("TurnTurret", turretRotation);
                }
        }

        void OnGUI() {
                if(unitSelection == null || !unitSelection.isSelected) {
                        return;
                }

                GUI.backgroundColor = stance == Stance.AGGRESSIVE ? Color.green : Color.white;
                if(GUI.Button(new Rect(0, Camera.main.pixelHeight - 80, 100, 20),
                              new GUIContent("Aggressive", "Shoot at enemy units and chase them indefinitely."))) {
                        ComSat.IssueSetDefenceStance(entity, Stance.AGGRESSIVE);
                }
                GUI.backgroundColor = stance == Stance.GUARD ? Color.green : Color.white;
                if(GUI.Button(new Rect(0, Camera.main.pixelHeight - 60, 100, 20),
                              new GUIContent("Guard", "Shoot at enemy units and chase them some distance."))) {
                        ComSat.IssueSetDefenceStance(entity, Stance.GUARD);
                }
                GUI.backgroundColor = stance == Stance.HOLD_GROUND ? Color.green : Color.white;
                if(GUI.Button(new Rect(0, Camera.main.pixelHeight - 40, 100, 20),
                              new GUIContent("Hold Ground", "Shoot at enemy units, but don't chase them."))) {
                        ComSat.IssueSetDefenceStance(entity, Stance.HOLD_GROUND);
                }
                GUI.backgroundColor = stance == Stance.HOLD_FIRE ? Color.green : Color.white;
                if(GUI.Button(new Rect(0, Camera.main.pixelHeight - 20, 100, 20),
                              new GUIContent("Hold Fire", "Do not shoot at enemy units unless ordered."))) {
                        ComSat.IssueSetDefenceStance(entity, Stance.HOLD_FIRE);
                }
                GUI.backgroundColor = Color.white;

                GUI.Label(new Rect(0,30,500,500), GUI.tooltip);
        }
}
