using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;
using Archaic.SoulDecay.Data;
using Sirenix.OdinInspector;
using Archaic.SoulDecay.Characters;
using DG.Tweening;
using Archaic.Core.Extensions;
using Archaic.Core.Utilities;

namespace Archaic.SoulDecay.Player.Movement
{
    public enum MoveState
    {
        Grounded,
        Airborne,
        WallRunning,
        Clinging,
        Swimming
    }

    public enum WallSide { Left, Right };

    [Serializable]
    public class MoveLogic : ICharacterController
    {
        public MoveState PreviousState;
        public MoveState CurrentState;

        public float TimeCurrentStateEntered = 0f;
        public float TimeSinceCurrentStateEntered => Time.time - TimeCurrentStateEntered;

        public MoveSettings CurrentSettings
        {
            get
            {
                switch (CurrentState)
                {
                    case MoveState.Grounded:
                        return GroundedSettings;
                    case MoveState.Airborne:
                        return AirborneSettings;
                    case MoveState.Clinging:
                    case MoveState.WallRunning:
                        return WallrunSettings;
                    case MoveState.Swimming:
                        return SwimmingSettings;
                    default:
                        return GroundedSettings;
                }
            }
        }

        [TabGroup("Settings", "Ground")]    public MoveSettings GroundedSettings;
        [TabGroup("Settings", "Airborne")]  public MoveSettings AirborneSettings;
        [TabGroup("Settings", "Wallrun")]   public MoveSettings_Wallrun WallrunSettings;
        [TabGroup("Settings", "Swimming")]  public MoveSettings SwimmingSettings;
        [TabGroup("Settings", "Jump")]      public JumpSettings jumpSettings;

        [Header("Crouch")]
        public float MaxCrouchedMoveSpeed = 7f;
        public float crouchSpeed = 0.3f;
        public float standingHeight = 1.5f;
        public bool CrouchedThisUpdate { get; private set; }

        [ShowInInspector, BoxGroup("Runtime Data")] public bool ShouldBeCrouching { get; set; }
        [ShowInInspector, BoxGroup("Runtime Data")] public bool IsCrouching { get; private set; }

        [ShowInInspector, BoxGroup("Runtime Data")] public bool JumpRequested { get; private set; }
        [ShowInInspector, BoxGroup("Runtime Data")] public bool JumpConsumed { get; private set; }

        public event Action<MoveState> ChangedState;
        public event Action PlayerJumped;

        public WallSide WallrunSide;

        public bool IgnoreExternalVelocity = false;
        public bool ShowDebug = false;

        public bool HasExternalForces => internalVelocityAdd.sqrMagnitude > 0 || internalVelocityForce.sqrMagnitude > 0;

        // References
        private PlayerData data;
        protected PlayerMotorController controller;

        // States
        private bool isNewStateRequested = false;
        private MoveState stateRequested;
        private bool justEnteredState = false;

        // Use for overlap checks
        protected Collider[] _probedColliders = new Collider[8];

        // Velocity
        private Vector3 internalVelocityAdd = Vector3.zero;
        private Vector3 internalVelocityForce = Vector3.zero;
        private bool velocityZeroRequested = false;
        private Vector3 velocityDiff = Vector3.zero;
        private Vector3 velocityAtStartOfFrame = Vector3.zero;

        // Wallrunning
        private Vector3 velocityOnStateEnter;
        private float heightLastFrame = 0f;
        private Vector3 lastRanAngle = Vector3.forward;
        private bool hasGroundedSinceLastWallRun = true;
        private float timeLastStaminaDrained = 0f;

        // Jumping
        private float timeSinceJumpRequested = 0f;
        private float timeSinceLastLeftGround = 0f;
        private bool jumpedThisFrame = false;

        // Shortcuts
        private WallStatus.WallData CurrentWallData => controller.WallStatus.CurrentStatus;

        /// <summary>
        /// Set up event references
        /// </summary>
        public virtual void Initialize(PlayerMotorController controller, PlayerData Data)
        {
            this.controller = controller;
            this.data = Data;

            if (data.Actor)
            {
                data.Actor.head.localPosition = Vector3.up * standingHeight;
            }

            controller.PlayerGrounded += OnPlayerGrounded;
        }

        public virtual void ProcessInput()
        {
            switch (CurrentState)
            {
                case MoveState.Grounded:
                    // Crouching input : handles toggle as well
                    ShouldBeCrouching = data.Input.Crouch;

                    // Jumping
                    if (data.Input.JumpDown && CanJump())
                    {
                        if (IsCrouching)
                        {
                            if (controller.CheckIfCanStand())
                                RequestJump();
                        }
                        else
                        {
                            RequestJump();
                        }
                    }
                    break;

                case MoveState.WallRunning:
                    if (data.Input.MoveAxisForward < 0 && ProfileManager.LoadedProfile.Data.abilities.CanWallCling)
                        RequestStateChange(MoveState.Clinging);

                    // Jumping
                    if (data.Input.JumpDown && CanJump())
                    {
                        if (CurrentWallData.GetLookAngle(data.Input.CameraForward) > WallrunSettings.approachAngleLimits.x)
                            RequestJump();
                        else if (ProfileManager.LoadedProfile.Data.abilities.CanWallCling)
                        {
                            RequestStateChange(MoveState.Clinging);
                        }
                    }

                    if (data.Input.CrouchDown)
                    {
                        RequestStateChange(MoveState.Airborne);
                    }
                    break;

                case MoveState.Clinging:
                    // Jumping
                    if (data.Input.JumpDown && CanJump())
                    {
                        RequestJump();
                    }

                    if (data.Input.CrouchDown)
                    {
                        RequestStateChange(MoveState.Airborne);
                    }
                    break;

                case MoveState.Airborne:
                    // One day I may implement crouch jumping
                    // But not today

                    // Crouching input : handles toggle as well
                    ShouldBeCrouching = data.Input.Crouch;

                    // Jumping
                    if (data.Input.JumpDown && CanJump())
                    {
                        RequestJump();
                    }
                    break;
                
                case MoveState.Swimming:
                    break;
            }

        }

        public bool CanJump()
        {
            return true;
        }

        public void RequestJump()
        {
            JumpRequested = true;
            timeSinceJumpRequested = 0f;
        }

        private void Jump(ref Vector3 currentVelocity)
        {
            // Uncrouch if crouched
            if (ShouldBeCrouching)
            {
                ShouldBeCrouching = false;
                Uncrouch();
            }

            PlayerJumped?.Invoke();

            PlayerController.AddBreadCrumb(PlayerController.CrumbCategory.Jump);
            GameInfo.I.Player.PlayerCamera.ArmatureAnimator.SetTrigger("Jump");
            GameInfo.I.Player.PlayerCamera.ArmatureAnimator.SetBool("IsGrounded", false);

            // Calculate jump direction before ungrounding
            Vector3 jumpDirection = controller.Motor.CharacterUp;
            Vector3 wallNormal = CurrentWallData.hitNormal;

            if (controller.Motor.GroundingStatus.FoundAnyGround && !controller.Motor.GroundingStatus.IsStableOnGround)
            {
                jumpDirection = controller.Motor.GroundingStatus.GroundNormal;
            }

            // Makes the character skip ground probing/snapping on its next update. 
            // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
            controller.Motor.ForceUnground(0.1f);

            // Add to the return velocity and reset jump state
            switch (CurrentState)
            {
                case MoveState.Grounded:
                    currentVelocity += (jumpDirection * jumpSettings.JumpSpeed * data.Ritual.GetMomentumData().JumpMultiplier) - Vector3.Project(currentVelocity, controller.Motor.CharacterUp);
                    break;

                case MoveState.Airborne:
                    currentVelocity += (jumpDirection.normalized * jumpSettings.JumpSpeed * data.Ritual.GetMomentumData().JumpMultiplier) - Vector3.Project(currentVelocity, controller.Motor.CharacterUp);
                    break;

                case MoveState.Clinging:
                case MoveState.WallRunning:
                    //jumpDirection = CurrentWallData.hitNormal + (controller.Motor.CharacterUp * jumpSettings.WallJumpVerticalModifier);

                    jumpDirection = data.Input.CameraForward.XZPlane() + (wallNormal * 0.25f);

                    float jumpVelocity = Mathf.Max(currentVelocity.magnitude, jumpSettings.JumpSpeed);

                    if (Vector3.Dot(jumpDirection, CurrentWallData.hitNormal) < 0)
                    {
                        // facing wall
                        currentVelocity = Vector3.Reflect((jumpDirection.normalized * jumpVelocity), wallNormal);
                    }
                    else
                    {
                        // not facing wall
                        currentVelocity = (jumpDirection.normalized * jumpVelocity);
                    }

                    currentVelocity += controller.Motor.CharacterUp * jumpSettings.WallJumpVerticalModifier * data.Ritual.GetMomentumData().JumpMultiplier;

                    data.Stamina.TrySpendStamina(jumpSettings.WallJumpStaminaCost);
                    break;

                case MoveState.Swimming:
                    break;
            }

            JumpRequested = false;
            JumpConsumed = true;
            jumpedThisFrame = true;

            data.Ritual.ritualPoints += 5;

            RequestStateChange(MoveState.Airborne);

            PlayJumpSound();
        }

        public void Crouch()
        {
            ShouldBeCrouching = true; // Unset by HandleInput
            IsCrouching = true;
            CrouchedThisUpdate = true; // unset next frame

            if (controller.Motor)
            {
                controller.Motor.SetCapsuleDimensions(controller.Motor.Capsule.radius, controller.CrouchColliderHeight, controller.CrouchColliderHeight / 2f);
            }

            // Ease head/camera
            if (data.Actor)
            {
                DOTween.To(
                    () => data.Actor.head.localPosition,
                    x => data.Actor.head.localPosition = x,
                    Vector3.up * controller.CrouchColliderHeight,
                    crouchSpeed)
                    .SetEase(Ease.OutBack);

                DOTween.To(
                    () => data.Actor.chest.localPosition,
                    x => data.Actor.chest.localPosition = x,
                    Vector3.up * (controller.CrouchColliderHeight - 0.25f),
                    crouchSpeed)
                    .SetEase(Ease.OutBack);
            }

            // Handle mesh (for debug purposes in editor)
            controller.MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
        }

        public void Uncrouch()
        {
            ShouldBeCrouching = false;
            IsCrouching = false;

            if (controller.Motor)
            {
                /*
                DOTween.To(
                    () => controller.Motor.Capsule.height,
                    x => { controller.Motor.SetCapsuleDimensions(controller.Motor.Capsule.radius, x, x / 2f); },
                    controller.CharacterColliderHeight,
                    crouchSpeed)
                    .SetEase(Ease.OutQuad);
                */
                controller.Motor.SetCapsuleDimensions(controller.Motor.Capsule.radius, controller.CharacterColliderHeight, controller.CharacterColliderHeight / 2f);
            }

            // Ease head/camera
            if (data.Actor)
            {
                DOTween.To(
                    () => data.Actor.head.localPosition,
                    x => data.Actor.head.localPosition = x,
                    Vector3.up * standingHeight,
                    crouchSpeed)
                    .SetEase(Ease.OutQuad);

                DOTween.To(
                    () => data.Actor.chest.localPosition,
                    x => data.Actor.chest.localPosition = x,
                    Vector3.up * (standingHeight - 0.25f),
                    crouchSpeed)
                    .SetEase(Ease.OutQuad);
            }

            // Handle mesh (for debug purposes in editor)
            controller.MeshRoot.localScale = new Vector3(1f, 1f, 1f);
        }

        public bool CanWallRun(out bool canWallrun, out bool canCling)
        {
            canWallrun = false;
            canCling = false;

            if (CurrentWallData.status != WallStatus.Status.FoundValidWall)
                return false;

            if (!PhysDataManager.GetData(CurrentWallData.collider.material, true).IsGrippable)
                return false;

            float approachAngle = CurrentWallData.GetApproachAngle(data.Actor.AvgVelocity);
            float lookAngle = CurrentWallData.GetLookAngle(data.Input.CameraForward);

            // Angle within wallrun range
            var sideHit = Vector3.Dot(-CurrentWallData.hitNormal, controller.Motor.CharacterRight) > 0 ? WallSide.Right : WallSide.Left;

            // Refuse to wallrun on the same side twice in a row.
            if (TimeSinceCurrentStateEntered < WallrunSettings.sameSideCooldown && !hasGroundedSinceLastWallRun && Vector3.Angle(lastRanAngle, -CurrentWallData.hitNormal.XZPlane()) < WallrunSettings.repeatLimitAngle)
            {
                return false;
            }

            lastRanAngle = -CurrentWallData.hitNormal.XZPlane();
            WallrunSide = sideHit;

            canWallrun = lookAngle < WallrunSettings.approachAngleLimits.y || lookAngle < 90;
            canCling = lookAngle <= WallrunSettings.approachAngleLimits.x;

            // No stamina left
            if (!data.Stamina.CanSpend(1))
                canWallrun = false;

            return (canWallrun || canCling);
        }

        public void BeforeCharacterUpdate(float deltaTime)
        {
            velocityAtStartOfFrame = controller.Motor.Velocity;

            switch (CurrentState)
            {
                case MoveState.Grounded:
                    CrouchedThisUpdate = false;
                    if (ShouldBeCrouching && !IsCrouching)
                    {
                        Crouch();
                    }
                    // Uncrouches in AfterCharacterUpdate
                    break;

                case MoveState.Airborne:
                    break;

                case MoveState.Clinging:
                    {
                        if (Time.time - timeLastStaminaDrained > WallrunSettings.clingStaminaDrain)
                        {
                            if (data.Stamina.TrySpendStamina(1))
                            {
                                timeLastStaminaDrained = Time.time;
                            }
                            else
                            {
                                RequestStateChange(MoveState.Airborne);
                                return;
                            }
                        }
                    }
                    break;

                case MoveState.WallRunning:
                    {
                        if (Time.time - timeLastStaminaDrained > WallrunSettings.wallrunStaminaDrain)
                        {
                            if (data.Stamina.TrySpendStamina(1))
                            {
                                timeLastStaminaDrained = Time.time;
                            }
                            else
                            {
                                RequestStateChange(MoveState.Airborne);
                                return;
                            }
                        }
                    }
                    break;

                case MoveState.Swimming:
                    break;
            }
        }

        public void AddVelocity(Vector3 velocityToAdd)
        {
            internalVelocityAdd += velocityToAdd;
        }

        public void SetContinuousVelocity(Vector3 velocityToAdd)
        {
            internalVelocityForce = velocityToAdd;
        }

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // Remove constant force from last frame so it does not add up
            if (!IgnoreExternalVelocity && internalVelocityForce.sqrMagnitude > 0f)
            {
                currentVelocity -= internalVelocityForce;
            }

            Vector3 targetMovementVelocity;

            // Jump
            jumpedThisFrame = false;
            timeSinceJumpRequested += deltaTime;

            if (JumpRequested && !JumpConsumed)
            {
                // Can jump based on current state
                switch (CurrentState)
                {
                    case MoveState.Grounded:
                        // If the player can jump while slipping, then any ground found will do, otherwise ground must be stable
                        if (jumpSettings.AllowJumpingWhenSlipping ? controller.Motor.GroundingStatus.FoundAnyGround : controller.Motor.GroundingStatus.IsStableOnGround)
                        {
                            Jump(ref currentVelocity);
                            return;
                        }
                        break;

                    case MoveState.Airborne:
                        if (TimeSinceCurrentStateEntered < jumpSettings.JumpPostGroundingGraceTime)
                        {
                            Jump(ref currentVelocity);
                            return;
                        }
                        break;

                    case MoveState.Clinging:
                    case MoveState.WallRunning:
                        Jump(ref currentVelocity);
                        return;
                }
            }
            else
            {
                switch (CurrentState)
                {
                    case MoveState.Grounded:
                        {
                            // Handle velocity
                            targetMovementVelocity = Vector3.zero;

                            // Reorient velocity on slope
                            currentVelocity = controller.Motor.GetDirectionTangentToSurface(currentVelocity, controller.Motor.GroundingStatus.GroundNormal) * currentVelocity.magnitude;

                            // Calculate target velocity
                            Vector3 inputRight = Vector3.Cross(data.Input.MoveVector_World, controller.Motor.CharacterUp);
                            Vector3 reorientedInput = Vector3.Cross(controller.Motor.GroundingStatus.GroundNormal, inputRight).normalized * data.Input.MoveVector_World.magnitude;
                            targetMovementVelocity = reorientedInput * (IsCrouching ? MaxCrouchedMoveSpeed : CurrentSettings.maxSpeed) * data.Ritual.GetMomentumData().SpeedMultiplier;

                            // Smooth movement Velocity
                            //currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1 - Mathf.Exp(-StableMovementSharpness * deltaTime));

                            velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, controller.Gravity);
                            currentVelocity += velocityDiff * CurrentSettings.acceleration * deltaTime;

                            // Drag
                            currentVelocity *= (1f / (1f + (CurrentSettings.friction * deltaTime)));
                        }
                        break;

                    case MoveState.Airborne:
                        {
                            targetMovementVelocity = Vector3.zero;

                            // Add move input
                            if (data.Input.MoveVector_World.sqrMagnitude > 0f)
                            {
                                targetMovementVelocity = data.Input.MoveVector_World * CurrentSettings.maxSpeed * data.Ritual.GetMomentumData().SpeedMultiplier;

                                // Prevent climbing on un-stable slopes with air movement
                                if (controller.Motor.GroundingStatus.FoundAnyGround)
                                {
                                    Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(controller.Motor.CharacterUp, controller.Motor.GroundingStatus.GroundNormal), controller.Motor.CharacterUp).normalized;
                                    targetMovementVelocity = Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                                }

                                velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, controller.Gravity);
                                currentVelocity += velocityDiff * CurrentSettings.acceleration * deltaTime;
                            }

                            // Gravity
                            currentVelocity += controller.Gravity * deltaTime;

                            // Drag
                            currentVelocity *= (1f / (1f + (CurrentSettings.friction * deltaTime)));
                        }
                        break;

                    case MoveState.WallRunning:
                        {
                            float speed = Mathf.Max(velocityOnStateEnter.XZPlane().magnitude, WallrunSettings.maxSpeed * data.Ritual.GetMomentumData().SpeedMultiplier);

                            // Reorient velocity on slope
                            currentVelocity = Vector3.ProjectOnPlane(currentVelocity, CurrentWallData.hitNormal).normalized; // Project onto wall
                            currentVelocity = Vector3.ProjectOnPlane(currentVelocity, controller.Motor.CharacterUp).normalized; // Project to direction

                            currentVelocity *= speed;

                            // Calculate wallrun height/curve
                            float heightMod = WallrunSettings.wallRunCurve.Evaluate(TimeSinceCurrentStateEntered / WallrunSettings.wallRunTime) * WallrunSettings.wallRunHeight * 100;

                            float heightAcceleration = heightMod - heightLastFrame;

                            currentVelocity = new Vector3(currentVelocity.x, heightAcceleration, currentVelocity.z);

                            heightLastFrame = heightMod;
                        }
                        break;

                    case MoveState.Clinging:
                        {
                            currentVelocity *= 1f / (1f + (CurrentSettings.friction * deltaTime));

                            // Weakened gravity
                            currentVelocity += controller.Gravity.normalized * WallrunSettings.gravityMultiplier * deltaTime;

                            currentVelocity = Vector3.ProjectOnPlane(currentVelocity, CurrentWallData.hitNormal); // Project onto wall
                        }
                        break;

                    case MoveState.Swimming:
                        break;
                }
            }

            // Take into account additive velocity
            if (!IgnoreExternalVelocity && internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += internalVelocityAdd;
                internalVelocityAdd = Vector3.zero;
            }

            // Take into account additive velocity
            if (!IgnoreExternalVelocity && internalVelocityForce.sqrMagnitude > 0f)
            {
                currentVelocity += internalVelocityForce;
            }
        }

        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentState)
            {
                case MoveState.Grounded:
                    // Jumping
                    {
                        // Handle jump-related values

                        // Handle jumping pre-ground grace period
                        if (JumpRequested && timeSinceJumpRequested > jumpSettings.JumpPreGroundingGraceTime)
                        {
                            JumpRequested = false;
                        }

                        if (!jumpedThisFrame && (jumpSettings.AllowJumpingWhenSlipping ? controller.Motor.GroundingStatus.FoundAnyGround : controller.Motor.GroundingStatus.IsStableOnGround))
                        {
                            // If we're on a ground surface, reset jumping values
                            JumpConsumed = false;
                        }
                    }
                    // Handle uncrouching
                    if (IsCrouching && !ShouldBeCrouching)
                    {
                        // Do an overlap test with the character's standing height to see if there are any obstructions
                        if (!controller.CheckIfCanStand())
                        {
                            // If obstructions, just stick to crouching dimensions
                            Crouch();
                        }
                        else
                        {
                            // If no obstructions, uncrouch
                            Uncrouch();
                        }
                    }
                    break;

                case MoveState.Airborne:
                    // If the player has been airborne for longer than the jump post grounding grace time, set request to false
                    if (JumpRequested && (TimeSinceCurrentStateEntered > jumpSettings.JumpPostGroundingGraceTime && timeSinceJumpRequested > jumpSettings.JumpPreGroundingGraceTime))
                        JumpRequested = false;
                    break;

                case MoveState.Clinging:
                case MoveState.WallRunning:
                    {
                        // Check if the wall is curving towards or away from the player
                        // Stop running if the curve is too steep
                        float deltaNormalAngle = Vector3.Angle(controller.WallStatus.LastStatus.hitNormal, CurrentWallData.hitNormal);
                        float dot = Vector3.Dot(controller.Motor.Velocity.XZPlane().normalized, CurrentWallData.hitNormal);

                        Debug.DrawLine(controller.transform.position, controller.transform.position + controller.Motor.Velocity.XZPlane().normalized, Color.white);
                        Debug.DrawLine(controller.transform.position, controller.transform.position + CurrentWallData.hitNormal, Color.blue);

                        if (deltaNormalAngle > 0 && dot > 0f)
                        {
                            Debug.Log("Curving Out");
                            // Curving outwards/Away from movement
                            if (deltaNormalAngle > WallrunSettings.maxOuterAngleChangeAllowed)
                            {
                                RequestStateChange(MoveState.Airborne);
                                return;
                            }
                        }
                        else if (deltaNormalAngle > 0 && dot < 0f)
                        {
                            Debug.Log("Curving In");
                            // Curving inwards/Towards movement
                            if (deltaNormalAngle > WallrunSettings.maxInnerAngleChangeAllowed)
                            {
                                RequestStateChange(MoveState.Airborne);
                                return;
                            }
                        }
                    }
                    break;

                case MoveState.Swimming:
                    break;
            }

            justEnteredState = false;
            CheckCurrentState();
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            if (CurrentState == MoveState.WallRunning && hitStabilityReport.IsStable)
            {
                RequestStateChange(MoveState.Grounded);
            }
        }

        /// <summary> Checks the current state of the character in the world and sets it accordingly </summary>
        public void CheckCurrentState()
        {
            if (isNewStateRequested)
            {
                ChangeState(stateRequested);
                isNewStateRequested = false;
                return;
            }

            switch (CurrentState)
            {
                case MoveState.Grounded:

                    break;

                case MoveState.Airborne:
                    {
                        bool shouldCling;
                        bool shouldWallrun;
                           
                        CanWallRun(out shouldWallrun, out shouldCling);

                        if ((shouldCling || shouldWallrun) && TimeSinceCurrentStateEntered > WallrunSettings.cooldown)
                        {
                            if (shouldCling && ProfileManager.LoadedProfile.Data.abilities.CanWallCling)
                            {
                                ChangeState(MoveState.Clinging);
                                return;
                            }
                            else if (shouldWallrun && ProfileManager.LoadedProfile.Data.abilities.CanWallRun)
                            {
                                ChangeState(MoveState.WallRunning);
                                return;
                            }
                        }

                        if (controller.Motor.GroundingStatus.IsStableOnGround && !controller.Motor.MustUnground())
                        {
                            ChangeState(MoveState.Grounded);
                            return;
                        }
                    }
                    break;

                case MoveState.WallRunning:
                    {
                        // If the vertical slope of the wall changes to be outside the limits, disengage.
                        float slopeAngle = Vector3.Angle(controller.Motor.CharacterUp, CurrentWallData.hitNormal);
                        if (slopeAngle < controller.WallStatus.slopeLimits.x || slopeAngle > controller.WallStatus.slopeLimits.y)
                        {
                            PlayerController.AddBreadCrumb(PlayerController.CrumbCategory.Position);
                            ChangeState(MoveState.Airborne);
                            return;
                        }

                        // If player has outran the time, disgenage.
                        if (TimeSinceCurrentStateEntered > WallrunSettings.wallRunTime)
                        {
                            PlayerController.AddBreadCrumb(PlayerController.CrumbCategory.Position);
                            ChangeState(MoveState.Airborne);
                            return;
                        }

                        // *** OLD COMMENT FOR (TimeSinceCurrentStateEntered > 1f)
                        // Only check if the player is above ground after a second or two
                        // this allows the player some time to leave the ground after initiating a low run
                        // ***
                        if (controller.WallStatus.IsCloseToGround)
                        {
                            PlayerController.AddBreadCrumb(PlayerController.CrumbCategory.Position);
                            ChangeState(MoveState.Airborne);
                            return;
                        }
                    }
                    break;

                case MoveState.Clinging:
                    {
                        // If the vertical slope of the wall changes to be outside the limits, disengage.
                        float slopeAngle = Vector3.Angle(controller.Motor.CharacterUp, CurrentWallData.hitNormal);
                        if (slopeAngle < controller.WallStatus.slopeLimits.x || slopeAngle > controller.WallStatus.slopeLimits.y)
                        {
                            ChangeState(MoveState.Airborne);
                            return;
                        }

                        // *** OLD COMMENT FOR (TimeSinceCurrentStateEntered > 1f)
                        // Only check if the player is above ground after a second or two
                        // this allows the player some time to leave the ground after initiating a low run
                        // ***
                        if (controller.WallStatus.IsCloseToGround)
                        {
                            ChangeState(MoveState.Airborne);
                            return;
                        }
                    }
                    break;

                case MoveState.Swimming:
                    break;
            }

            // Go to airborn state if not on ground, or jumped and not wallrunning or clinging
            if (CurrentState == MoveState.Grounded && 
                (!controller.Motor.GroundingStatus.IsStableOnGround || controller.Motor.MustUnground()))
            {
                ChangeState(MoveState.Airborne);
                return;
            }

            // Perform the state change
            void ChangeState(MoveState newState)
            {
                switch (CurrentState)
                {
                    case MoveState.Grounded:
                        break;
                    case MoveState.Airborne:
                        break;
                    case MoveState.WallRunning:
                        GameInfo.I.Player.PlayerCamera.ArmatureAnimator.SetBool("Wallrunning", false);
                        break;
                    case MoveState.Clinging:
                        break;
                    case MoveState.Swimming:
                        break;
                }

                switch (newState)
                {
                    case MoveState.Grounded:
                        controller.InvokePlayerGrounded(this, new PlayerMotorController.PlayerGroundedEventArgs()
                        {
                            HitNormal = controller.Motor.GroundingStatus.GroundNormal,
                            IsHardGrounding = controller.Motor.GroundingStatus.IsStableOnGround
                        });
                        JumpConsumed = false;
                        if (IsCrouching)
                            CrouchedThisUpdate = true;
                        break;

                    case MoveState.Airborne:
                        GameInfo.I.Player.PlayerCamera.ArmatureAnimator.SetBool("IsGrounded", false);
                        break;

                    case MoveState.WallRunning:
                        controller.InvokePlayerGrounded(this, new PlayerMotorController.PlayerGroundedEventArgs()
                        {
                            IsHardGrounding = true,
                            HitNormal = controller.WallStatus.CurrentStatus.hitNormal
                        });
                        hasGroundedSinceLastWallRun = false;
                        SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.BeginWallRun, controller.transform, Vector3.zero, false);
                        GameInfo.I.Player.PlayerCamera.ArmatureAnimator.SetBool("Wallrunning", true);
                        JumpConsumed = false;

                        //data.Stamina.SpendStamina(1);
                        break;

                    case MoveState.Clinging:
                        controller.InvokePlayerGrounded(this, new PlayerMotorController.PlayerGroundedEventArgs()
                        {
                            IsHardGrounding = true,
                            HitNormal = controller.WallStatus.CurrentStatus.hitNormal
                        });
                        hasGroundedSinceLastWallRun = false;
                        JumpConsumed = false;

                        // If horizontal momentum is over a non significant number, play a scrape sound
                        if (velocityAtStartOfFrame.XZPlane().magnitude > 4f)
                        {
                            SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.ClingStart, controller.transform, Vector3.zero, false);
                        }
                        break;

                    case MoveState.Swimming:
                        break;
                }

                TimeCurrentStateEntered = Time.time;
                heightLastFrame = 0f;
                velocityOnStateEnter = controller.Motor.Velocity;
                justEnteredState = true;
                ChangedState?.Invoke(newState);
                PreviousState = CurrentState;
                CurrentState = newState;
            }
        }

        public void RequestStateChange(MoveState newState)
        {
            if (newState == CurrentState)
                return;

            isNewStateRequested = true;
            stateRequested = newState;
        }

        private void PlayJumpSound()
        {
            if (CurrentState == MoveState.WallRunning)
            {
                SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.WallJump, controller.Motor.transform, Vector3.up, false);
            }
            else
            {
                SECTR_AudioSystem.Play(GlobalData.Instance.Player.Sounds.Jump, controller.transform, Vector3.zero, false);
            }

            PhysicMaterial material;
            if (controller.Motor.GroundingStatus.FoundAnyGround)
                material = controller.Motor.GroundingStatus.GroundCollider.material;
            else if (CurrentWallData.IsWallTouched)
                material = CurrentWallData.collider.material;
            else
                return;

            PhysData data = PhysDataManager.GetData(material, true);

            if (data)
            {
                SECTR_AudioSystem.Play(data.sfxJumpCue, controller.transform.position, false);
            }
        }
        private void OnPlayerGrounded(object sender, PlayerMotorController.PlayerGroundedEventArgs args)
        {
            // Reset wallrun
            if (args.IsHardGrounding)
                hasGroundedSinceLastWallRun = true;

            // Tell animator player is grounded
            GameInfo.I.Player.PlayerCamera.ArmatureAnimator.SetBool("IsGrounded", true);
        }

        #region Unimplemented Interface Methods

        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime) { }

        public void PostGroundingUpdate(float deltaTime) { }

        public bool IsColliderValidForCollisions(Collider coll) { return true; }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }

        public void OnDiscreteCollisionDetected(Collider hitCollider) { }
        #endregion Unimplemented Interface Methods
    }
}