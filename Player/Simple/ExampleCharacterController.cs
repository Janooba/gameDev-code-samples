using Archaic.Core.Extensions;
using Archaic.PhysicsMetadata;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ExampleCharacterController : MonoBehaviour
{
    public struct GroundData
    {
        public PhysData physData;
        public RaycastHit hit;
    }

    public CharacterController controller;
    public Transform head;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float sprintSpeed = 5f;
    public float moveSharpness = 15f;
    public float drag = 0.1f;
    public float gravity = 30f;
    public float jumpStrength = 10f;

    [Header("Rotation")]
    public Vector2 lookSensitivity = new Vector2(1, 1);
    public float minVerticalAngle = -90f;
    public float maxVerticalAngle = 90f;

    [Header("Rigidbody Interaction")]
    public float characterMass = 80f;
    public float pushForceMultiplier = 1f;
    public float groundedMultiplier = 3f;

    // events
    public event System.Action<RaycastHit> PlayerJumped;
    public event System.Action<RaycastHit, float> PlayerLanded;

    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 moveInputVector;

    private float headDirection = 0f;
    private float headAngle = 0f;

    // Properties
    public bool IsSprinting { get; private set; }
    public bool IsMoving => moveInputVector.sqrMagnitude > 0;
    public bool IsGrounded { get; private set; }
    private bool wasGrounded = false;
    public GroundData GroundStatus { get; private set; }

    public Vector3 CapsuleCenter => transform.position + controller.center;
    public Vector3 CapsuleTopHemiCenter => CapsuleCenter + (transform.up * (controller.height / 2 - controller.radius));
    public Vector3 CapsuleBottomHemiCenter => CapsuleCenter + (-transform.up * (controller.height / 2 - controller.radius));

    public Vector3 CapsuleTopHemi => CapsuleCenter + (transform.up * controller.height / 2);
    public Vector3 CapsuleBottomHemi => CapsuleCenter + (-transform.up * controller.height / 2);

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateRotation();
        UpdateMovement();
    }

    private void FixedUpdate()
    {
        SweepVelocity(transform.TransformVector(currentVelocity * Time.fixedDeltaTime));
        controller.Move(transform.TransformVector(currentVelocity * Time.fixedDeltaTime));
    }

    private void UpdateRotation()
    {
        float horizRotation = Input.GetAxisRaw("Mouse X");
        float vertRotation = -Input.GetAxisRaw("Mouse Y");

        horizRotation *= lookSensitivity.x * 1;
        vertRotation *= lookSensitivity.y * 1;

        headDirection += horizRotation;
        headAngle += vertRotation;

        headAngle = Mathf.Clamp(headAngle, minVerticalAngle, maxVerticalAngle);

        transform.rotation = Quaternion.Euler(0, headDirection, 0);
        head.localRotation = Quaternion.Euler(headAngle, 0, 0);
    }

    private void UpdateMovement()
    {
        StoreGroundData();

        DebugCanvas.Instance.UpdateValue("Ground Material", GroundStatus.physData.name);

        IsSprinting = Input.GetKey(KeyCode.LeftShift);
        float strafeInput = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
        float forwardInput = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
        moveInputVector = new Vector3(strafeInput, 0, forwardInput).normalized;

        wasGrounded = IsGrounded;
        IsGrounded = controller.isGrounded;

        if (IsGrounded && !wasGrounded) // Landed
        {
            PlayerLanded?.Invoke(GroundStatus.hit, currentVelocity.magnitude);
        }

        if (IsGrounded)
        {
            Vector3 targetVelocity = moveInputVector * (IsSprinting ? sprintSpeed : moveSpeed);

            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-moveSharpness * Time.deltaTime));
        }
        else
        {
            Vector3 addedVelocity = moveInputVector * moveSpeed;
            Vector3 currentHorizVelocity = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);

            if (currentHorizVelocity.magnitude < moveSpeed)
            {
                addedVelocity = Vector3.ClampMagnitude(currentHorizVelocity + addedVelocity, moveSpeed) - currentHorizVelocity;
            }
            else
            {
                if (Vector3.Dot(currentHorizVelocity, addedVelocity) > 0f)
                {
                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentHorizVelocity.normalized);
                }
            }

            currentVelocity += addedVelocity;
        }

        // Jumping
        if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            currentVelocity += Vector3.up * jumpStrength;
            PlayerJumped?.Invoke(GroundStatus.hit);
        }

        // Gravity
        currentVelocity += Vector3.down * gravity * Time.deltaTime;

        if (!controller.isGrounded) // Drag
            currentVelocity *= 1f / (1f + (drag * Time.deltaTime));
    }

    private RaycastHit[] groundHits = new RaycastHit[1];
    private void StoreGroundData()
    {
        int hits = Physics.SphereCastNonAlloc(CapsuleBottomHemiCenter, controller.radius, -transform.up, groundHits, controller.skinWidth);

        if (hits > 0)
        {
            for (int i = 0; i < hits; i++)
            {
                if (groundHits[i].collider.CompareTag("Player"))
                    continue;

                Material mat = groundHits[i].GetMaterial();
                if (PhysDataManager.TryGetData(mat, out PhysData data, groundHits[i].collider.material))
                {
                    GroundStatus = new GroundData
                    {
                        physData = data,
                        hit = groundHits[i]
                    };

                    return;
                }
            }
        }

        GroundStatus = new GroundData
        {
            physData = PhysDataManager.GetDefault(),
            hit = new RaycastHit()
        };
    }

    private RaycastHit[] sweepHits = new RaycastHit[5];

    private void SweepVelocity(Vector3 velocity)
    {
        velocity += velocity.normalized * controller.skinWidth;

        if (IsGrounded)
        {
            velocity = velocity.XZPlane();
        }

        DebugExtension.DebugCapsule(CapsuleTopHemi, CapsuleBottomHemi, Color.red, controller.radius);
        DebugExtension.DebugCapsule(CapsuleTopHemi + velocity, CapsuleBottomHemi + velocity, Color.green, controller.radius);

        int hits = Physics.CapsuleCastNonAlloc(CapsuleTopHemiCenter, CapsuleBottomHemiCenter, controller.radius, velocity.normalized, sweepHits, velocity.magnitude);
        if (hits > 0)
        {
            for (int i = 0; i < hits; i++)
            {
                if (sweepHits[i].rigidbody)
                {
                    sweepHits[i].rigidbody.AddForceAtPosition(velocity * characterMass * pushForceMultiplier * (IsGrounded ? groundedMultiplier : 1f), sweepHits[i].point);
                    DebugExtension.DebugArrow(sweepHits[i].point, velocity * 10);
                }
            }
        }
    }
}
