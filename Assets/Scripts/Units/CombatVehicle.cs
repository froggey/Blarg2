﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Entity), typeof(Vehicle))]
public class CombatVehicle : MonoBehaviour {
        public GameObject projectilePrefab;

        [HideInInspector]
        public Mode mode;
        [HideInInspector]
        public DVector2 destination; // Current movement target.
        [HideInInspector]
        public Entity target; // Current attack target.
        private Entity[] targets;
        private bool movingToTarget; // Cleared when attackDistance is reached.

        private Entity entity;
        private Vehicle vehicle;

        public EditableDReal _attackDistance;
        private DReal attackDistance { get { return _attackDistance.Parse(); } }
        public EditableDReal _attackRange;
        private DReal attackRange { get { return _attackRange.Parse(); } }
        public EditableDReal _turretTurnSpeed;
        private DReal turretTurnSpeed { get { return DReal.Radians(_turretTurnSpeed.Parse()); } }
        static readonly DReal sqrPositioningAccuracy = (DReal)1 / 100;

        [HideInInspector]
        public DReal turretRotation;

        public bool turretAutoResets;

        void Awake() {
                ComSat.Trace(this, "Awake");
                entity = GetComponent<Entity>();
                entity.AddUpdateAction(TickUpdate);
                vehicle = GetComponent<Vehicle>();

                mode = Mode.IDLE;
        }

        public enum Mode {
                IDLE, MOVE, ATTACK
        }

        void Attack(Entity[] targets) {
                ComSat.Trace(this, "Attack");
                mode = Mode.ATTACK;
                target = null;
                this.targets = targets;
                movingToTarget = false;
                vehicle.Stop();
        }

        void Move(DVector2 location) {
                ComSat.Trace(this, "Move");
                mode = Mode.MOVE;
                target = null;
                targets = null;
                destination = location;
        }

        private void PickNewTarget() {
                if (targets == null) targets = new Entity[] {};
                targets = targets.Where(t => ComSat.EntityExists(t)).OrderBy(t => (t.position - entity.position).sqrMagnitude).ToArray();
                if (targets.Count() > 0) {
                        target = targets[0];
                        mode = Mode.ATTACK;
                } else {
                        target = null;
                        mode = Mode.IDLE;
                }
        }

        void TickUpdate() {
                ComSat.Trace(this, "TickUpdate");
                if(mode == Mode.ATTACK && !ComSat.EntityExists(target)) {
                        PickNewTarget();
                        if (!ComSat.EntityExists(target)) vehicle.Stop();
                }

                if(mode == Mode.ATTACK) {
                        var distVec = target.position - entity.position;
                        var dist = distVec.magnitude;

                        DReal targetTurretAngle;

                        var projectileProjectile = projectilePrefab != null ? projectilePrefab.GetComponent<Projectile>() : null;
                        if(projectileProjectile != null) {
                                var aimSpot = Utility.PredictShot(entity.position, projectileProjectile.initialSpeed,
                                                                  target.position, target.velocity);

                                targetTurretAngle = DReal.Mod(DVector2.ToAngle(aimSpot - entity.position) - entity.rotation, DReal.TwoPI);
                        } else {
                                targetTurretAngle = DReal.Mod(DVector2.ToAngle(distVec) - entity.rotation, DReal.TwoPI);
                        }

                        // Turn turret to point at target when close.
                        if(dist >= attackRange * 2) {
                                targetTurretAngle = turretAutoResets ? 0 : turretRotation;
                        }
                        turretRotation = Utility.CalculateNewAngle(turretRotation, targetTurretAngle, turretTurnSpeed);
                        SendMessage("TurnTurret", turretRotation);

                        if(dist < attackDistance) {
                                // Close enough.
                                movingToTarget = false;
                                vehicle.Stop();
                        } else if(movingToTarget || (dist >= attackRange)) {
                                movingToTarget = true;
                                // Approach target.
                                vehicle.MoveTowards(target.position);
                        }

                        // Fire when in range and pointing the gun at the target.
                        if(dist < attackRange && targetTurretAngle == turretRotation) {
                                SendMessage("Fire");
                        }
                } else if(mode == Mode.MOVE) {
                        // Move towards.
                        if((destination - entity.position).sqrMagnitude < sqrPositioningAccuracy) {
                                // Close enough.
                                mode = Mode.IDLE;
                                vehicle.Stop();
                        } else {
                                vehicle.MoveTowards(destination);
                        }
                        if (turretAutoResets) {
                                turretRotation = Utility.CalculateNewAngle(turretRotation, 0, turretTurnSpeed);
                                SendMessage("TurnTurret", turretRotation);
                        }
                } else if(mode == Mode.IDLE) {
                        vehicle.Stop();
                        if (turretAutoResets) {
                                turretRotation = Utility.CalculateNewAngle(turretRotation, 0, turretTurnSpeed);
                                SendMessage("TurnTurret", turretRotation);
                        }
                }
        }
}
