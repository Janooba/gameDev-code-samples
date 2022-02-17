using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using KinematicCharacterController;
using Archaic.Core.Extensions;
using Sirenix.OdinInspector;
using Archaic.SoulDecay.Player.Movement;
using Archaic.SoulDecay.Data;
using Archaic.SoulDecay.Characters;
using Archaic.Core.Utilities;
using System;
using QFSW.QC;

// This beast of a script handles all of the players movement logic.
// Combined with the MoveLogic.cs for normal movement and Ability System.

// Uses "Kinematic Character Controller" from the asset store to handle
// the actual collision solving. A portion of the code here is from the
// KCC asset, as this needs to adhere to the KCC API

namespace Archaic.SoulDecay.Player
{
    public class PlayerMotorController : SerializedMonoBehaviour, ICharacterController
    {
        public KinematicCharacterMotor Motor;

        [Header("Misc")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        public float OrientationSharpness = 10;
        public bool OrientTowardsGravity = false;
        public Vector3 Gravity = new Vector3(0, -30f, 0);
        public Transform MeshRoot;
        public float CharacterColliderHeight = 1.8f;
        public float CrouchColliderHeight = 0.8f;

        [FoldoutGroup("Movement Logic"), HideLabel]
        public MoveLogic MovementLogic;

        [FoldoutGroup("Wall Status"), HideLabel]
        public WallStatus WallStatus;

        public bool PlayFootsteps => ActiveAbilities.Length > 0 && !ActiveAbilities.Any(x => x.PlayFootsteps == true);

        [SerializeField]
        public List<StateAbility> Abilities = new List<StateAbility>();

        public event Action<StateAbility> OnAbilityActivated;
        public void InvokeAbilityActivated(StateAbility ability) { OnAbilityActivated?.Invoke(ability); }
        public StateAbility[] ActiveAbilities => Abilities.Where(x => x.IsActive).ToArray();
        public bool AnyAbilitiesActive => Abilities.Any(x => x.IsActive);

        private Collider[] _probedColliders = new Collider[8];

        // Events
        public class PlayerGroundedEventArgs : EventArgs
        {
            public bool IsHardGrounding { get; set; }
            public Vector3 HitNormal { get; set; }
        }
        public event EventHandler<PlayerGroundedEventArgs> PlayerGrounded;
        public void InvokePlayerGrounded(object sender, PlayerGroundedEventArgs args) => PlayerGrounded?.Invoke(sender, args);

        // Velocity
        private Vector3 _internalVelocityAdd = Vector3.zero;
        private bool velocityZeroRequested = false;

        private PlayerData data;

        public T GetAbilityOfType<T>()
        {
            return Abilities.OfType<T>().FirstOrDefault();
        }

        public void Initialize(PlayerController controller)
        {
            // Assign to motor
            Motor.CharacterController = this;
            Motor.enabled = true;
            data = controller.Data;

            MovementLogic.Initialize(this, controller.Data);
            WallStatus.Initialize(this, controller.Data);

            foreach (var ability in Abilities)
            {
                ability.Initialize(this, controller.Data);
            }
        }

        /// <summary>
        /// This is called every frame by MyPlayer in order to tell the character what its inputs are
        /// </summary>
        public void SetInputs()
        {
            #region Update state and abilities
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.ProcessInput();

            // Run the abilities
            foreach (var ability in Abilities)
            {
                ability.SetInputs();
            }
            #endregion Update state and abilities
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called before the character begins its movement update
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            #region Update state and abilities
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.BeforeCharacterUpdate(deltaTime);

            bool stopOtherAbilities = false;
            foreach (var ability in Abilities)
            {
                ability.PassiveUpdate(deltaTime);
                if (ability.CanStartAbility(ActiveAbilities, MovementLogic.CurrentState) && ability.SelfStarts)
                {
                    if (stopOtherAbilities && ability.CanStopAbility())
                        ability.StopAbility();

                    ability.StartAbility();

                    if (ability.StopOtherAbilities)
                        stopOtherAbilities = true;
                }
            }

            // Run the abilities until you find one that's not able to passthrough
            foreach (var ability in Abilities)
            {
                if (!ability.IsActive)
                    continue;

                ability.BeforeCharacterUpdate(deltaTime);

                if (!ability.IsPassthrough)
                    break;
            }
            #endregion Update state and abilities

            WallStatus.UpdateStatus();
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its rotation should be right now. 
        /// This is the ONLY place where you should set the character's rotation
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (data.Input.CameraForward.XZPlane() != Vector3.zero && OrientationSharpness > 0f)
            {
                // Smoothly interpolate from current to target look direction
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, data.Input.CameraForward.XZPlane().normalized, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                // Set the current rotation (which will be used by the KinematicCharacterMotor)
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }

            #region Update state and abilities
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.UpdateRotation(ref currentRotation, deltaTime);

            // Run the abilities until you find one that's not able to passthrough
            foreach (var ability in Abilities)
            {
                if (!ability.IsActive)
                    continue;

                ability.UpdateRotation(ref currentRotation, deltaTime);

                if (!ability.IsPassthrough)
                    break;
            }
            #endregion Update state and abilities

            if (OrientTowardsGravity)
            {
                // Rotate from current up to invert gravity
                currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -Gravity) * currentRotation;
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its velocity should be right now. 
        /// This is the ONLY place where you can set the character's velocity
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            #region Update state and abilities
            if (velocityZeroRequested)
            {
                velocityZeroRequested = false;
                currentVelocity = Vector3.zero;
                return;
            }

            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.UpdateVelocity(ref currentVelocity, deltaTime);

            // Run the abilities until you find one that's not able to passthrough
            foreach (var ability in Abilities)
            {
                if (!ability.IsActive)
                    continue;

                ability.UpdateVelocity(ref currentVelocity, deltaTime);

                if (!ability.IsPassthrough)
                    break;
            }
            #endregion Update state and abilities

            // Take into account additive velocity
            if (MovementLogic.IgnoreExternalVelocity && _internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called after the character has finished its movement update
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            #region Update state and abilities
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.AfterCharacterUpdate(deltaTime);
            else
                MovementLogic.CheckCurrentState();

            // Run the abilities until you find one that's not able to passthrough
            foreach (var ability in Abilities)
            {
                if (!ability.IsActive)
                    continue;

                ability.AfterCharacterUpdate(deltaTime);

                if (!ability.IsPassthrough)
                    break;
            }
            #endregion Update state and abilities
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (IgnoredColliders.Contains(coll))
            {
                return false;
            }
            else if (MovementLogic.IsColliderValidForCollisions(coll))
            {
                return true;
            }
            else
                foreach (var ability in Abilities)
                {
                    if (ability.IsActive && ability.IsColliderValidForCollisions(coll))
                        return true;
                }
            return false;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            #region Update state and abilities
            MovementLogic.OnGroundHit(hitCollider, hitNormal, hitPoint, ref hitStabilityReport);

            foreach (var ability in Abilities)
            {
                ability.OnGroundHit(hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
            }
            #endregion Update state and abilities
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
            #region Update state and abilities
            MovementLogic.OnMovementHit(hitCollider, hitNormal, hitPoint, ref hitStabilityReport);

            foreach (var ability in Abilities)
            {
                ability.OnMovementHit(hitCollider, hitNormal, hitPoint, ref hitStabilityReport);
            }
            #endregion Update state and abilities
        }

        public void AddVelocity(Vector3 velocity)
        {
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.AddVelocity(velocity);
        }

        public void SetContinuousVelocity(Vector3 velocity)
        {
            // If any ability is active but also not set to pass through, don't run the move state
            MovementLogic.SetContinuousVelocity(velocity);
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
            #region Update state and abilities
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.ProcessHitStabilityReport(hitCollider, hitNormal, hitPoint, atCharacterPosition, atCharacterRotation, ref hitStabilityReport);

            // Run the abilities until you find one that's not able to passthrough
            foreach (var ability in Abilities)
            {
                if (!ability.IsActive)
                    continue;

                ability.ProcessHitStabilityReport(hitCollider, hitNormal, hitPoint, atCharacterPosition, atCharacterRotation, ref hitStabilityReport);

                if (!ability.IsPassthrough)
                    break;
            }
            #endregion Update state and abilities
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            #region Update state and abilities
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.PostGroundingUpdate(deltaTime);

            // Run the abilities until you find one that's not able to passthrough
            foreach (var ability in Abilities)
            {
                if (!ability.IsActive)
                    continue;

                ability.PostGroundingUpdate(deltaTime);

                if (!ability.IsPassthrough)
                    break;
            }
            #endregion Update state and abilities
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
            #region Update state and abilities
            // If any ability is active but also not set to pass through, don't run the move state
            if (!Abilities.Any(i => i.IsActive && !i.IsPassthrough))
                MovementLogic.OnDiscreteCollisionDetected(hitCollider);

            // Run the abilities until you find one that's not able to passthrough
            foreach (var ability in Abilities)
            {
                if (!ability.IsActive)
                    continue;

                ability.OnDiscreteCollisionDetected(hitCollider);

                if (!ability.IsPassthrough)
                    break;
            }
            #endregion Update state and abilities
        }

        /// <summary>
        /// Checks if the player can stand at its current position.
        /// </summary>
        /// <returns>True if the area is clear to stand up</returns>
        public bool CheckIfCanStand()
        {
            float radius = Motor.Capsule.radius;
            float height = Motor.Capsule.height;
            float yOffset = Motor.Capsule.center.y;

            Motor.SetCapsuleDimensions(radius, CharacterColliderHeight, CharacterColliderHeight / 2f);

            bool isOverlapping = Motor.CharacterOverlap(
                Motor.TransientPosition,
                Motor.TransientRotation,
                _probedColliders,
                Motor.CollidableLayers,
                QueryTriggerInteraction.Ignore) > 0;

            Motor.SetCapsuleDimensions(radius, height, yOffset);

            return !isOverlapping;
        }

        public void RequestZeroVelocity()
        {
            velocityZeroRequested = true;
        }

        private void OnDrawGizmos()
        {
            Matrix4x4 original = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;
            if (!Motor)
                return;

            DebugExtension.DrawCapsule(Motor.CharacterTransformToCapsuleTop, Motor.CharacterTransformToCapsuleBottom, Motor.Capsule.radius);

            Gizmos.matrix = original;
        }
    }
}