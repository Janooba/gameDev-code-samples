using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using Sirenix.OdinInspector;
using System;
using Archaic.SoulDecay.Data;
using DG.Tweening;

namespace Archaic.SoulDecay.Player.Movement
{
    [Serializable]
    public class Dash : StateAbility
    {
        // If has Dash ability, time is past recharge, is on stable ground, or can use while airborne
        public bool CanDash => ProfileManager.LoadedProfile.Data.abilities.CanDash && _timeSinceLastDashed > dashSettings.rechargeTime;

        public DashSettings dodgeSettings;
        public DashSettings dashSettings;

        public DashSettings ActiveSettings {
            get
            {
                if (CanDash)
                    return dashSettings;
                else
                    return dodgeSettings;
            }
        }

        public float DashStatus => Mathf.Clamp01(_timeSinceLastDashed / dashSettings.rechargeTime);

        private Vector3 requestedDashDirection;
        private Vector3 _initialVelocity;
        private Vector3 _currentDashVelocity;
        private bool _isStopped;
        private bool _mustStopVelocity = false;
        private float _timeSinceStartedDash = 0;

        private bool _hasGroundedSinceLastDash = true;
        private float _timeSinceLastDashed = 0f;
        private float currentFOVChange = 0;

        private bool wallHitDetected = false;

        public override void Initialize(PlayerMotorController motorController, PlayerData data)
        {
            base.Initialize(motorController, data);

            base.motorController.PlayerGrounded += OnPlayerGrounded;
        }

        public override bool CanStartAbility(StateAbility[] activeAbilities, MoveState currentState)
        {
            if (!base.CanStartAbility(activeAbilities, currentState))
                return false;

            if (IsActive)
                return false;

            if (!_hasGroundedSinceLastDash)
                return false;

            if (_timeSinceLastDashed < ActiveSettings.rechargeTime)
                return false;

            if (!Motor.GroundingStatus.IsStableOnGround && !ActiveSettings.canUseWhileAirborne)
                return false;

            if (!data.Input.DashDown || data.Input.MoveVector_Local.sqrMagnitude == 0)
                return false;

            if (!data.Stamina.CanSpend(ActiveSettings.staminaCost))
                return false;

                return true;
        }

        public override void SetInputs()
        {
            base.SetInputs();
            requestedDashDirection = new Vector3(data.Input.MoveAxisRight, 0, data.Input.MoveAxisForward).normalized;
        }

        public override void StartAbility()
        {
            base.StartAbility();

            foreach (var ability in motorController.ActiveAbilities)
            {
                if (ability == this)
                    continue;

                if (ability.CanStopAbility())
                    ability.StopAbility();
            }

            _initialVelocity = motorController.Motor.Velocity;
            //_currentDashVelocity = controller.Motor.CharacterForward * ActiveSettings.Speed;

            _currentDashVelocity = motorController.Motor.Transform.TransformDirection(requestedDashDirection) * ActiveSettings.speed * GameInfo.I.Player.Data.Ritual.GetMomentumData().SpeedMultiplier;

            _timeSinceStartedDash = 0f;
            motorController.Motor.ForceUnground(0.1f);

            //controller.TransitionToMoveState(controller.airborneState);

            _isStopped = false;
            _mustStopVelocity = false;
            _hasGroundedSinceLastDash = false;
            wallHitDetected = false;

            GameInfo.I.Player.Actor.Health.isInvulnerable = true;

            data.Stamina.TrySpendStamina(ActiveSettings.staminaCost);

            if (CanDash)
            {
                //GameInfo.I.Player.Data.Ritual.IncreaseMomentum(1);
                SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.Dash, motorController.transform, Vector3.zero, false);
            }
            else
            {
                SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.Dodge, motorController.transform, Vector3.zero, false);
            }

            TraumaData.InvokeRecievedTraumaEvent(this, ActiveSettings.screenShake, false, "movement");
            PlayerController.AddBreadCrumb(PlayerController.CrumbCategory.Dash);

            currentFOVChange = ActiveSettings.fovChange;
            PlayerController.Instance.PlayerCamera.ModifyFOV(currentFOVChange);

            PPVolumes.I.dashVolume.weight = 1f;
        }

        public override void PassiveUpdate(float deltaTime)
        {
            _timeSinceLastDashed += deltaTime;
        }

        public override void BeforeCharacterUpdate(float deltaTime)
        {
            // Update times
            _timeSinceStartedDash += deltaTime;
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // If we have stopped and need to cancel velocity, do it here
            if (_mustStopVelocity)
            {
                currentVelocity = _currentDashVelocity.normalized * motorController.MovementLogic.CurrentSettings.maxSpeed;
                _mustStopVelocity = false;
                _isStopped = true;
            }
            else if (!_isStopped)
            {
                // When charging, velocity is always constant
                currentVelocity = _currentDashVelocity;
            }
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            // Detect being stopped by elapsed time
            if (!_isStopped && _timeSinceStartedDash > ActiveSettings.maxTime)
            {
                _mustStopVelocity = true;
                _isStopped = true;
            }
            else if (_isStopped)
            {
                StopAbility();
            }

            if (wallHitDetected)
            {
                //motorController.TransitionToMoveState(motorController.wallState);
                // go to wall
                wallHitDetected = false;
            }
        }

        public override void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            if (IsActive)
            {
                // Detect being stopped by obstructions
                if (!_isStopped && !hitStabilityReport.IsStable && Vector3.Dot(-hitNormal, _currentDashVelocity.normalized) > 0.5f)
                {
                    _mustStopVelocity = true;

                    // If airborn, try to wallrun/cling
                    if (motorController.MovementLogic.CurrentState == MoveState.Airborne)
                    {
                        //wallHitDetected = motorController.UpdateWallData();
                    }
                }
            }
            else
            {

            }
        }

        public override void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            if (IsActive)
            {


            }
            else
            {
                if (hitStabilityReport.IsStable)
                    _hasGroundedSinceLastDash = true;
            }
        }

        public void OnPlayerGrounded(object sender, PlayerMotorController.PlayerGroundedEventArgs args)
        {
            ResetAbility();
        }

        public override void StopAbility()
        {
            base.StopAbility();

            // Effects
            PlayerController.Instance.PlayerCamera.ModifyFOV(-currentFOVChange);
            currentFOVChange = 0;
            DOTween.To(() => PPVolumes.I.dashVolume.weight,
                (x) => { PPVolumes.I.dashVolume.weight = x; },
                0f,
                0.2f);
            PPVolumes.I.dashVolume.weight = 1f;

            _timeSinceLastDashed = 0f;
            PlayerController.AddBreadCrumb(PlayerController.CrumbCategory.Position);
            GameInfo.I.Player.Actor.Health.isInvulnerable = false;
        }

        public void ResetAbility()
        {
            _hasGroundedSinceLastDash = true;
        }
    }
}