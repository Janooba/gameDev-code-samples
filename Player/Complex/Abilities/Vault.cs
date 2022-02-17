using KinematicCharacterController;
using Sirenix.OdinInspector;
using System;
using UnityEngine;
using Archaic.Core.Utilities;
using Archaic.SoulDecay.Data;
using Archaic.Core.Extensions;

namespace Archaic.SoulDecay.Player.Movement
{
    [Serializable]
    public class Vault : StateAbility
    {
        public VaultSettings settings;

        private Vector3 cameraPoint => data.Input.CameraPosition_World;
        private Vector3 ledgeTestPoint => (Vector3.up * settings.ledgeCheckHeight) + data.Actor.head.position + (data.Input.CameraForward.XZPlane().normalized * settings.ledgeCheckDistance);

        private bool canClimb = false;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        
        /// <summary> The detected ledge point </summary>
        private Vector3 ledgePoint;

        private float currentClimbTime = 0f;

        private bool shouldStopClimbing = false;
        private bool isTryingToMoveForward = false;

        private Animator armatureAnimator => GameInfo.I.Player.PlayerCamera.ArmatureAnimator;

        public override void Initialize(PlayerMotorController motorController, PlayerData data)
        {
            base.Initialize(motorController, data);
        }

        public override bool CanStartAbility(StateAbility[] activeAbilities, MoveState currentState)
        {
            if (IsActive)
                return false;

            if (motorController.MovementLogic.CurrentState == MoveState.Grounded)
                return false;

            canClimb = CheckIfSurfaceValid();

            if (motorController.MovementLogic.IsCrouching)
                return false;

            if (motorController.GetAbilityOfType<GrappleHook>().IsActive)
                return false;

            return canClimb && isTryingToMoveForward;
        }

        [Button]
        public override void StartAbility()
        {
            base.StartAbility();

            var clamber = motorController.GetAbilityOfType<Clamber>();
            if (clamber != null && clamber.IsActive)
            {
                clamber.StopAbility();
            }

            startPosition = Motor.Transform.position;
            targetPosition = ledgePoint;

            currentClimbTime = 0f;
            Motor.SetCapsuleCollisionsActivation(false);
            Motor.SetMovementCollisionsSolvingActivation(false);

            SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.Vault, data.Input.CameraTransform, Vector3.zero, false);
            armatureAnimator.SetBool("Vaulting", true);
        }

        public override void PassiveUpdate(float deltaTime)
        {
            if (ShowDebug && canClimb)
                DebugExtension.DebugCapsule(ledgePoint, ledgePoint + (Motor.CharacterUp * Motor.Capsule.height), Motor.Capsule.radius);

            if (ShowDebug)
                DebugExtension.DebugWireSphere(ledgeTestPoint, Color.blue, 0.1f);
        }

        public override void SetInputs()
        {
            base.SetInputs();

            // So that you only climb if you're deliberately trying to move forward
            isTryingToMoveForward = data.Input.MoveAxisForward > 0;
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            currentClimbTime += deltaTime;
            currentClimbTime = Mathf.Clamp(currentClimbTime, 0f, settings.climbTime);

            Vector3 currTarget = Vector3.Lerp(startPosition, targetPosition, currentClimbTime / settings.climbTime);

            currentVelocity = Motor.GetVelocityForMovePosition(Motor.TransientPosition, currTarget, deltaTime);

            if (Vector3.Distance(Motor.Transform.position, targetPosition) < 0.01f)
                shouldStopClimbing = true;
        }

        public override void AfterCharacterUpdate(float deltaTime)
        {
            if (shouldStopClimbing)
                StopAbility();
        }

        public override void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {

        }

        public override bool CanStopAbility()
        {
            return true;
        }

        public override void StopAbility()
        {
            base.StopAbility();

            shouldStopClimbing = false;
            Motor.SetCapsuleCollisionsActivation(true);
            Motor.SetMovementCollisionsSolvingActivation(true);
            armatureAnimator.SetBool("Vaulting", false);
        }

        private bool CheckIfSurfaceValid()
        {
            RaycastHit hit;
            // Make sure there is space infront of the player
            if (Physics.Raycast(data.Actor.head.position, data.Input.CameraForward.XZPlane().normalized, out hit, settings.ledgeCheckDistance, settings.worldLayerMask, QueryTriggerInteraction.Ignore))
                return false;

            // Raycast downwards towards the floor, the distance is the players head from the players feet, minus a little bit
            if (Physics.Raycast(ledgeTestPoint, Vector3.down, out hit, data.Actor.head.localPosition.y - settings.ledgeDepthCheckDistance, settings.worldLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (ShowDebug)
                    DebugExtension.DebugArrow(ledgeTestPoint, hit.point - ledgeTestPoint, Color.blue);

                // Leeway for slopes, so the player doesnt barely get stuck within
                ledgePoint = hit.point + (Vector3.up * 0.1f);

                // return false if the ledge is too steep
                if (Vector3.Angle(hit.normal, Motor.CharacterUp) > settings.maxLedgeSlope)
                    return false;

                // Checks if the final position is valid
                probedColliders = new Collider[2];
                int overlapped = Motor.CharacterOverlap(ledgePoint, Motor.TransientRotation, probedColliders, settings.worldLayerMask, QueryTriggerInteraction.Ignore);

                if (overlapped > 0)
                {
                    // If the player overlaps anything, then the position is not valid
                    return false;
                }

                return true;
            }
            else
                DebugExtension.DebugArrow(ledgeTestPoint, Vector3.down * (data.Actor.head.localPosition.y - settings.ledgeDepthCheckDistance), Color.blue);

            return false;
        }
    }
}