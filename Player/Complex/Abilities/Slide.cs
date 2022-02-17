using KinematicCharacterController;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

using Archaic.SoulDecay.Data;
using Archaic.Core.Extensions;

namespace Archaic.SoulDecay.Player.Movement
{
    [Serializable]
    public class Slide : StateAbility
    {
        public SlideSettings settings;

        [ShowInInspector]
        public float Velocity => motorController ? Motor.Velocity.magnitude : 0f;

        // Slide ability.
        private Vector3 currentSlideVelocity;
        private Vector3 initialSlideVelocity;
        private float currentInputDirection;
        private bool mustStopVelocity = false;
        private bool isStarting = false;
        private bool startedThisFrame = false;
        [ShowInInspector, ReadOnly]
        private bool slideRequested = false;
        private float timeSlideWasRequested = 0f;
        private PhysData currentPhysData;
        private SECTR_AudioCueInstance slideLoop_Player;
        private SECTR_AudioCueInstance slideLoop_Physics;
        private SECTR_AudioCueInstance slideLoop_Directional;

        public void RequestSlide()
        {
            slideRequested = true;
            timeSlideWasRequested = Time.time;
        }

        [ShowInInspector]
        public bool IsSlidePending => slideRequested && Time.time - timeSlideWasRequested < settings.SlidePreGroundingGraceTime;

        public override bool CanStartAbility(StateAbility[] activeAbilities, MoveState currentState)
        {
            if (IsActive)
                return false;

            if (!ProfileManager.LoadedProfile.Data.abilities.CanCombatSlide)
                return false;

            if (data.Input.CrouchDown || slideRequested)
            {
                if (currentState != MoveState.Grounded)
                    return false;

                if (Motor.Velocity.magnitude < settings.MinSpeedForSlide)
                    return false;

                if (motorController.MovementLogic.IsCrouching && !motorController.MovementLogic.CrouchedThisUpdate)
                    return false;

                return true;
            }
            else
                return false;
        }

        public override void StartAbility()
        {
            base.StartAbility();

            mustStopVelocity = false;
            isStarting = true;
            startedThisFrame = true;
            slideRequested = false;

            currentSlideVelocity = Motor.Velocity;
            initialSlideVelocity = Motor.Velocity;

            motorController.MovementLogic.Crouch();

            SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.SlideStart, motorController.transform, Vector3.zero, false);

            currentPhysData = PhysDataManager.GetData(Motor.GroundingStatus.GroundCollider.material, true);

            slideLoop_Player =      SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.SlideLoop, motorController.transform, Vector3.zero, true);
            slideLoop_Physics =     SECTR_AudioSystem.Play(currentPhysData.sfxSlideLoopCue, motorController.transform, Vector3.zero, true);
            slideLoop_Directional = SECTR_AudioSystem.Play(currentPhysData.sfxSlideDirectionalLoopCue, motorController.transform, Vector3.zero, true);

            motorController.Invoke(() => TraumaData.InvokeRecievedTraumaEvent(this, settings.slideTrauma, false, "movement"), settings.slideTraumaDelay);

            //PlayerController.Instance.cameraController.ModifyFOV(settings.FOVChange);
        }

        public override void BeforeCharacterUpdate(float deltaTime)
        {
            // Check ground physdata. If the ground type changes, stop the slide sounds and start new ones.
            PhysData physData = currentPhysData = PhysDataManager.GetData(Motor.GroundingStatus.GroundCollider.material, true);
            if (currentPhysData != physData)
            {
                slideLoop_Player.Stop(false);
                slideLoop_Physics.Stop(false);
                slideLoop_Directional.Stop(false);

                currentPhysData = physData;

                slideLoop_Player =      SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.SlideLoop, motorController.transform, Vector3.zero, true);
                slideLoop_Physics =     SECTR_AudioSystem.Play(currentPhysData.sfxSlideLoopCue, motorController.transform, Vector3.zero, true);
                slideLoop_Directional = SECTR_AudioSystem.Play(currentPhysData.sfxSlideDirectionalLoopCue, motorController.transform, Vector3.zero, true);
            }
        }

        public override void SetInputs()
        {
            base.SetInputs();

            if (IsActive)
            {
                if (!isStarting)
                {
                    if ((!settings.ToggleSlide && data.Input.CrouchUp) || data.Input.CrouchDown)
                    {
                        //mustStopVelocity = true;
                        StopAbility();
                    }
                }

                // Handle canceling slide to jump
                if (data.Input.JumpDown &&
                    motorController.MovementLogic.CanJump() &&
                    CanStopAbility())
                {
                    StopAbility();
                    motorController.MovementLogic.RequestJump();
                }

                // Stop sliding if no longer crouching
                if (!data.Input.Crouch)
                {
                    StopAbility();
                }

                currentInputDirection = data.Input.MoveAxisRight;
            }
            else
            {
                // Hold crouch to initiate slide when you land
                switch (motorController.MovementLogic.CurrentState)
                {
                    case MoveState.Airborne:
                        slideRequested = data.Input.Crouch;
                        break;
                }
            }
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            timeSlideWasRequested += deltaTime;

            // If we have stopped and need to cancel velocity, do it here
            if (mustStopVelocity)
            {
                currentVelocity = Vector3.zero;
                return;
            }

            // Calculating variables needed for later
            Vector3 downSlopeDirection = Vector3.ProjectOnPlane(motorController.Gravity, Motor.GroundingStatus.GroundNormal);
            Vector3 downSlopeCross = Vector3.Cross(-motorController.Gravity.normalized, downSlopeDirection).normalized;

            float slopeCoef = settings.FrictionSlopeCoef.Evaluate(Vector3.Angle(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal) / 90f);

            // Using A and D to adjust aim by tilting gravity
            Vector3 newGravity = motorController.Gravity;
            if (currentInputDirection != 0)
            {
                Vector3 inputDirection = Motor.CharacterRight * currentInputDirection * settings.MaxMovementContribution;
                newGravity = (motorController.Gravity.normalized + inputDirection).normalized * motorController.Gravity.magnitude;
            }

            // Gravity
            Vector3 gravityAcceleration = Vector3.ProjectOnPlane(newGravity, Motor.GroundingStatus.GroundNormal) * deltaTime;

            // Calculating Drag
            Vector3 frictionDrag = -Motor.Velocity.normalized * (settings.Friction * slopeCoef) * deltaTime;

            Vector3 sideFrictionDrag = Vector3.zero;

            // If the slope is shallow enough (flat) then don't bother trying to correct sideways motion
            if (slopeCoef < 0.95f)
                sideFrictionDrag = -Vector3.Project(Motor.Velocity, downSlopeCross) * settings.SideFriction * deltaTime;

            currentVelocity += gravityAcceleration + frictionDrag + sideFrictionDrag;


            // Slowing down the player if they exceed max speed
            if (currentVelocity.magnitude > settings.MaxSlideSpeed)
            {
                currentVelocity -= gravityAcceleration;
            }

            // Start boost
            if (startedThisFrame && settings.StartBoost > 0f)
            {
                currentVelocity += currentVelocity.normalized * settings.StartBoost;
            }

            float currSpeedPercent = Mathf.InverseLerp(settings.MinSpeedForSlide, settings.MaxSlideSpeed, currentVelocity.magnitude);

            slideLoop_Player.Pitch = Mathf.Pow(Mathf.Lerp(settings.slidePitchRange.x, settings.slidePitchRange.y, currSpeedPercent), 2f);
            slideLoop_Physics.Pitch = Mathf.Pow(Mathf.Lerp(settings.slidePitchRange.x, settings.slidePitchRange.y, currSpeedPercent), 2f);
            slideLoop_Directional.Volume = Mathf.Lerp(slideLoop_Directional.Volume, Mathf.Abs(data.Input.MoveAxisRight), 10f * deltaTime);
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (Motor.Velocity.magnitude < settings.SlideStopSpeed || 
                !Motor.GroundingStatus.FoundAnyGround || 
                mustStopVelocity)
            {
                StopAbility();
            }

            if (TimeSinceStarted > settings.MinSlideTime)
                isStarting = false;

            startedThisFrame = false;
        }

        public override void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            if (!IsActive)
                return;

            // Detect being stopped by obstructions
            if (!hitStabilityReport.IsStable && Vector3.Dot(-hitNormal, currentSlideVelocity.normalized) > 0.5f)
            {
                mustStopVelocity = true;
                TraumaData.InvokeRecievedTraumaEvent(this, settings.slideTrauma, false, "movement");
            }
        }

        public override bool CanStopAbility()
        {
            return motorController.CheckIfCanStand();
        }

        public override void StopAbility()
        {
            base.StopAbility();

            motorController.MovementLogic.ShouldBeCrouching = data.Input.Crouch;

            slideLoop_Player.Stop(false);
            slideLoop_Physics.Stop(false);
            slideLoop_Directional.Stop(false);

            // Reset values
            mustStopVelocity = false;

            //PlayerController.Instance.cameraController.ModifyFOV(-settings.FOVChange);
        }
    }
}