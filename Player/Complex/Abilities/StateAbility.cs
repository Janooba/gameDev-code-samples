using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using Archaic.SoulDecay.Data;

namespace Archaic.SoulDecay.Player.Movement
{
    [Serializable]
    public abstract class StateAbility : ICharacterController
    {
        protected PlayerMotorController motorController;
        protected KinematicCharacterMotor Motor => motorController.Motor;
        protected PlayerData data;

        public bool SelfStarts = true;
        public bool IgnoreExternalVelocity = false;
        public bool IsPassthrough = true;
        public bool StopOtherAbilities = false;
        public bool PlayFootsteps = false;
        public bool IsActive = false;
        public bool ShowDebug = false;


        protected float TimeStarted = 0f;
        protected float TimeSinceStarted => Time.time - TimeStarted;
        // Use for overlap checks
        protected Collider[] probedColliders = new Collider[8];

        public virtual void Initialize(PlayerMotorController motorController, PlayerData data)
        {
            this.motorController = motorController;
            this.data = data;
        }

        public virtual bool CanStartAbility(StateAbility[] activeAbilities, MoveState currentState)
        {
            return true;
        }

        public virtual void StartAbility()
        {
            IsActive = true;
            TimeStarted = Time.time;
            motorController.InvokeAbilityActivated(this);
        }

        public virtual bool CanStopAbility()
        {
            return true;
        }

        public virtual void StopAbility()
        {
            IsActive = false;
        }

        public virtual void SetInputs() { }

        public virtual void PassiveUpdate (float deltaTime) { }

        public virtual void BeforeCharacterUpdate(float deltaTime) { }

        public virtual void UpdateRotation(ref Quaternion currentRotation, float deltaTime) { }

        public virtual void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime) { }

        public virtual void AfterCharacterUpdate(float deltaTime) { }

        public virtual bool IsColliderValidForCollisions(Collider coll) { return true; }

        /// <summary>
        ///  ability is active or now
        /// </summary>
        /// <param name="hitCollider"></param>
        public virtual void OnDiscreteCollisionDetected(Collider hitCollider) { }

        /// <summary>
        /// This is called when the motor's ground probing detects a ground hit. Called even when ability is inactive
        /// </summary>
        public virtual void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        /// <summary>
        /// This is called when the motor's movement logic detects a hit. Called even when ability is inactive
        /// </summary>
        public virtual void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        public virtual void PostGroundingUpdate(float deltaTime) { }

        public virtual void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    }
}