using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using Peg.GCCI;
using System;
using static Peg.CharacterController.ICharacterController;

namespace Peg.CharacterController
{
    /// <summary>
    ///     
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterControllerSimple : MonoBehaviour, ICharacterController, IMover, IJumper, IGravity
    {
        [Header("Links")]
        public RigidbodyGroundedDetector GroundDetector;
        public PhysicMaterial MovingFriction;
        public PhysicMaterial StationaryFriction;
        public Collider FrictionSwapTargets;

        [Header("Stats")]
        public float Acceleration = 60;
        public float MaxSpeed = 4;
        public float BrakingForce = 5;
        public float JumpForce = 7;
        [SerializeField]
        float _GravityScale = 1.0f;
        public float GravityScale { get => _GravityScale; set => _GravityScale = value; }
        [Range(0, 1)]
        public float AirControl = 0.25f;
        [Range(0, 1)]
        public float JumpCutoff = 0.5f;


        bool RequestJump;
        public bool Jumping;
        Rigidbody Body;
        Vector3 MoveImpulse;

        //IMPORTANT NOTE: The first element in both of these arrays is simply there to make the math easier. It should never change!
        readonly float[] SpeedBonuses = { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };      //10 bonuses plus sprinting
        readonly float[] SpeedPenalties = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }; //10 pentalties plus the walk and crouch

        //properties
        float SpeedAccum(float total, float current) => total * current;
        public float EffectiveAcceleration => Acceleration * SpeedBonuses.Sum() * SpeedPenalties.Aggregate(1.0f, SpeedAccum);
        public float EffectiveMaxSpeed => MaxSpeed * SpeedBonuses.Sum() * SpeedPenalties.Aggregate(1.0f, SpeedAccum);
        public Vector3 MovementVelocity => new(Body.velocity.x, 0, Body.velocity.z);
        public Vector3 GravityVelocity { get => new(0, Body.velocity.y, 0); set => Body.velocity = new Vector3(Body.velocity.x, value.y, Body.velocity.z); }
        public MovementMode MoveMode { get => MovementMode.Walking; set => throw new NotImplementedException(); }
        public MaxSpeedLimitMethod SpeedLimitMethod => MaxSpeedLimitMethod.DirectVelocitySet;
        public Rigidbody RigidbodyComp => throw new NotImplementedException();

        /// <summary>
        /// 
        /// </summary>
        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.sleepThreshold = -1;
        }

        #region Interfaces
        public bool MoveEnabled { get; set; } = true;

        public float InputX
        {
            set
            {
                MoveImpulse = new Vector3(value, 0, MoveImpulse.y);
            }
        }

        public float InputY
        {
            set
            {
                MoveImpulse = new Vector3(MoveImpulse.x, 0, value);
            }
        }

        public bool JumpEnabled { get; set; } = true;

        public bool JumpInput
        {
            set
            {
                if (value && GroundDetector.IsGrounded)
                    RequestJump = true;
            }
        }

        public bool CanJump { get { throw new NotImplementedException(); } }
        public bool IsJumping { get { throw new NotImplementedException(); } }
        public bool JumpedThisFrame { get { throw new NotImplementedException(); } }
        public bool IsFallingFromJump { get { throw new NotImplementedException(); } }
        public float JumpWindow { get { throw new NotImplementedException(); } }

        public void Step(float deltaTime)
        {
            throw new NotImplementedException();
        }

        public bool GravityEnabled { get; set; } = true;

        public Vector3 Velocity { get => Body.velocity; }
        #endregion


        #region Input Handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void OnMove(InputValue value)
        {
            if (!MoveEnabled) return;

            var input = value.Get<Vector2>();
            MoveImpulse = new Vector3(input.x, 0, input.y);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void OnJump(InputValue value)
        {
            if (!JumpEnabled) return;

            if (value.isPressed && GroundDetector.IsGrounded)
                RequestJump = true;
        }
        #endregion


        #region Bonuses & Penalties
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int AddSpeedBonus(float value)
        {
            for (int i = 1; i < SpeedBonuses.Length; i++)
            {
                if (SpeedBonuses[i] <= 0)
                {
                    SpeedBonuses[i] = value;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle"></param>
        public void RemoveSpeedBonus(int handle)
        {
            //we don't allow removing the default '1' from the first element as that's needed for the math to work
            if (handle < 1 || handle >= SpeedBonuses.Length)
                return;

            SpeedBonuses[handle] = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int AddSpeedPenalty(float value)
        {
            for (int i = 1; i < SpeedPenalties.Length; i++)
            {
                if (SpeedPenalties[i] >= 1)
                {
                    SpeedPenalties[i] = value;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle"></param>
        public void RemoveSpeedPenalty(int handle)
        {
            //we don't allow removing the default '1' from the first element as that's needed for the math to work
            if (handle < 1 || handle >= SpeedPenalties.Length)
                return;

            SpeedPenalties[handle] = 1;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        void ApplyPhysicsMaterials()
        {
            //adjust friction based on player input
            if (MoveImpulse.sqrMagnitude <= 0.01f && !GroundDetector.IsOnSlope && (!Jumping || !JumpedLastFrame))
                FrictionSwapTargets.sharedMaterial = StationaryFriction;
            else FrictionSwapTargets.sharedMaterial = MovingFriction;
        }

        /// <summary>
        /// 
        /// </summary>
        Vector3 ApplyMovement()
        {
            //calculate user input impulse (i.e., everything but outside forces like gravity, wind, explosions, etc...)
            var impulse = Body.rotation * MoveImpulse * EffectiveAcceleration;
            impulse *= GroundDetector.HasBeenGrounded && !GroundDetector.IsOnSlope ? 1 : AirControl;
            //if(SlopeCompensation) impulse = GroundDetector.SlopeCompensation(impulse, Body.position);
            Body.AddForce(impulse, ForceMode.Acceleration);

            return impulse;
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyLimitVelocity()
        {
            if (MovementVelocity.sqrMagnitude > EffectiveMaxSpeed * EffectiveMaxSpeed)
                Body.velocity = (MovementVelocity.normalized * EffectiveMaxSpeed) + GravityVelocity;
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyGravity()
        {
            if (!GravityEnabled) return;

            Vector3 grav = Physics.gravity;
            Body.AddForce(grav * GravityScale, ForceMode.Acceleration);
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyJump()
        {
            if (RequestJump)
            {
                JumpedLastFrame = true;
                Jumping = true;
                RequestJump = false;
                GroundDetector.ResetGroundedTimer(); //ensures we don't count ourselves as having been grounded recently
                Body.velocity = new Vector3(Body.velocity.x, JumpForce, Body.velocity.z);
            }
        }

        public LayerMask GroundLayers;
        public float ScanLength = 0.5f;

        bool JumpedLastFrame = true;
        int StepsSinceLastGrounded;
        bool GluedToFloor()
        {
            if (StepsSinceLastGrounded > 1)
                return false;

            if (!Physics.Raycast(Body.position + Vector3.up, Vector3.down, out RaycastHit hitInfo, 1 + ScanLength, GroundLayers, QueryTriggerInteraction.Ignore))
                return false;

            //check to see if on steep slope
            var floorDot = hitInfo.normal.y;
            if (floorDot < GroundDetector.SlopeAngleDot)
                return false;

            float speed = Body.velocity.magnitude;
            float dot = Vector3.Dot(Body.velocity, hitInfo.normal);
            if (dot > 0)
                Body.velocity = (Body.velocity - hitInfo.normal * dot).normalized * speed;
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private void FixedUpdate()
        {
            if (Jumping == true && GroundDetector.IsGrounded)
            {
                if (JumpedLastFrame)
                    JumpedLastFrame = false;
                else Jumping = false;
            }


            ApplyPhysicsMaterials();
            ApplyMovement();
            ApplyLimitVelocity();
            ApplyGravity();
            ApplyJump();

            StepsSinceLastGrounded++;
            if (!Jumping && (GroundDetector.IsGrounded || GluedToFloor()))
            {
                StepsSinceLastGrounded = 0;
            }
        }

    }
}
