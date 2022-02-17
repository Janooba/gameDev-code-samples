using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;
using Archaic.SoulDecay.Data;

namespace Archaic.SoulDecay.Player.Movement
{
    [Serializable]
    public class AirJump : StateAbility
    {
        [Header("Jump")]
        public float JumpSpeed = 10f;
        public int airJumpsAllowed => ProfileManager.LoadedProfile.Data.abilities.AdditionalAirJumps;
        public int staminaCost = 1;
        public float screenShake = 0.5f;
        public string screenShakeTag = "subtle";

        // Jumping
        private float Cooldown => motorController.MovementLogic.jumpSettings.JumpPostGroundingGraceTime;
        private bool jumpRequested = false;
        private bool jumpedThisFrame = false;
        private int jumpsConsumed = 0;

        public override void Initialize(PlayerMotorController motorController, PlayerData data)
        {
            base.Initialize(motorController, data);

            base.motorController.PlayerGrounded += OnPlayerLanded;
        }

        public override bool CanStartAbility(StateAbility[] activeAbilities, MoveState currentState)
        {
            if (IsActive)
                return false;

            if (jumpsConsumed >= airJumpsAllowed)
                return false;

            if (currentState != MoveState.Airborne)
                return false;

            if (motorController.MovementLogic.TimeSinceCurrentStateEntered < Cooldown)
                return false;

            if (!data.Stamina.CanSpend(staminaCost))
                return false;

            return data.Input.JumpDown;
        }

        public override void StartAbility()
        {
            base.StartAbility();

            jumpRequested = true;
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            jumpedThisFrame = false;

            // See if we actually are allowed to jump
            if (jumpRequested && jumpsConsumed < airJumpsAllowed)
            {
                TraumaData.InvokeRecievedTraumaEvent(this, screenShake, false, screenShakeTag);
                // Calculate jump direction
                Vector3 jumpDirection = Motor.CharacterUp;

                // Add to the return velocity and reset jump state
                currentVelocity += (jumpDirection * JumpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                jumpRequested = false;
                jumpsConsumed++;
                jumpedThisFrame = true;
                data.Stamina.TrySpendStamina(staminaCost);

                SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.AirJump, motorController.transform, Vector3.zero, false);

                data.Ritual.ritualPoints += 5;
            }
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            // Handle jump-related values

            if (jumpRequested)
            {
                jumpRequested = false;

                IsActive = false;
            }

            // reset jumping values
            if (!jumpedThisFrame)
            {
                IsActive = false;
            }
        }

        public override void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            base.OnMovementHit(hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
        }

        public void OnPlayerLanded(object sender, PlayerMotorController.PlayerGroundedEventArgs args)
        {
            if (args.IsHardGrounding)
                ResetAbility();
        }

        public void ResetAbility()
        {
            jumpRequested = false;
            jumpsConsumed = 0;
        }
    }
}