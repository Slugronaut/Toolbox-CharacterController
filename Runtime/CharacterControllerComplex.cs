using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using Toolbox.GCCI;
using System;
using static Toolbox.CharacterController.ICharacterController;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Toolbox.CharacterController
{
    /// <summary>
    /// TODO:
    ///     -Aim smoothing and adjustmen
    ///     -Wall Climbing
    ///     
    ///     
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CharacterControllerComplex : MonoBehaviour, ICharacterController, IMover, IJumper, IGravity
    {
        [Header("Links")]
        public RigidbodyGroundedDetector GroundDetector;
        public PhysicMaterial MovingFriction;
        public PhysicMaterial StationaryFriction;
        public Collider FrictionSwapTargets;

        [Header("Move Stats")]
        [SerializeField]
        MovementMode _MoveMode;
        public MovementMode MoveMode => _MoveMode;
        public float Acceleration = 60;
        public float MaxSpeed = 4;
        public float FlyAcceleration = 30;
        public float MaxFlySpeed = 4;

        [Header("Limits")]
        [Range(0, 1)]
        public float Dampening = 0;
        public float FlyingDampening = 3;
        public float BrakingForce = 5;
        [SerializeField]
        MaxSpeedLimitMethod _MaxSpeedLimit = MaxSpeedLimitMethod.SubtractiveVelocity;
        public MaxSpeedLimitMethod SpeedLimitMethod => _MaxSpeedLimit;

        [Header("Jump Stats")]
        public float JumpForce = 7;
        public float JumpFudgeTime = 0.1f;
        public int AirJumps = 0;
        [SerializeField]
        float _GravityScale = 1.0f;
        public float GravityScale { get => _GravityScale; set => _GravityScale = value; }
        [Range(0, 1)]
        public float AirControl = 0.25f;
        [Range(0, 1)]
        public float JumpCutoff = 0.5f;

        [Header("Movement Details")]
        [Tooltip("If set, when walking on inclines that are not considerd slopes, the gravity vector will be adjusted to match the normal of the walking surface. Otherwise gravity will always be set to the global value.")]
        public bool GravityMatchesFloor = true;
        public bool GlueToFloor = true;
        public LayerMask GroundLayers;
        public float ScanLength = 0.5f;



        //private
        const float MinSpeedSqr = 0.01f;
        Rigidbody Body;
        Vector3 MoveImpulse;
        bool JumpGravityLatch;
        int JumpInc;
        bool Jumping;
        float JumpPressTimer = 0;
        bool JumpedLastFrame;
        int StepsSinceLastGrounded;

        //IMPORTANT NOTE: The first element in both of these arrays is simply there to make the math easier. It should never change!
        readonly float[] SpeedBonuses = { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };      //10 bonuses plus sprinting
        readonly float[] SpeedPenalties = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }; //10 pentalties plus the walk and crouch

        //properties
        float SpeedAccum(float total, float current) => total * current;
        public float EffectiveAcceleration => Acceleration * SpeedBonuses.Sum() * SpeedPenalties.Aggregate(1.0f, SpeedAccum);
        public float EffectiveMaxSpeed => MaxSpeed * SpeedBonuses.Sum() * SpeedPenalties.Aggregate(1.0f, SpeedAccum);
        public Vector3 MovementVelocity => new(Body.velocity.x, 0, Body.velocity.z);
        public Vector3 GravityVelocity { get => new(0, Body.velocity.y, 0); set => Body.velocity = new Vector3(Body.velocity.x, value.y, Body.velocity.z); }


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
                if (MoveMode == MovementMode.Walking)
                {
                    if (value)
                    {
                        JumpPressTimer = Time.time;
                        if (GroundDetector.HasBeenGrounded)
                            JumpGravityLatch = true;
                    }
                    else if (Body.velocity.y > 0)
                    {
                        GroundDetector.ResetGroundedTimer(); //needed or we'll get a second jump
                        Body.velocity = new(Body.velocity.x, Body.velocity.y * JumpCutoff, Body.velocity.z);
                    }
                }
            }
        }

        public bool CanJump { get { throw new NotImplementedException(); } }
        public bool IsJumping => Jumping;
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

            if (MoveMode == MovementMode.Walking)
            {
                if (value.isPressed)
                {
                    JumpPressTimer = Time.time;
                    if (GroundDetector.HasBeenGrounded)
                        JumpGravityLatch = true;
                }
                else if (Body.velocity.y > 0)
                {
                    GroundDetector.ResetGroundedTimer(); //needed or we'll get a second jump
                    Body.velocity = new(Body.velocity.x, Body.velocity.y * JumpCutoff, Body.velocity.z);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void OnFly(InputValue value)
        {
            if (!MoveEnabled) return;
            if (MoveMode == MovementMode.Flying)
            {
                //we are instead, going to use this as a one-dimensional vector where 'jump' adds and 'crouch' subtracts
                JumpPressTimer = value.Get<float>();
            }

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
            if (MoveMode == MovementMode.Walking)
            {
                //adjust friction based on player input
                if (MoveImpulse.sqrMagnitude <= MinSpeedSqr && !GroundDetector.IsOnSlope && (!Jumping || !JumpedLastFrame))
                    FrictionSwapTargets.sharedMaterial = StationaryFriction;
                else FrictionSwapTargets.sharedMaterial = MovingFriction;
            }
            else
            {
                FrictionSwapTargets.sharedMaterial = MovingFriction;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        Vector3 ApplyMovement()
        {
            //calculate user input impulse (i.e., everything but outside forces like gravity, wind, explosions, etc...)
            var impulse = Body.rotation * MoveImpulse * EffectiveAcceleration;
            impulse *= MoveMode == MovementMode.Flying || (GroundDetector.HasBeenGrounded && !GroundDetector.IsOnSlope) ? 1 : AirControl;
            //if(SlopeCompensation) impulse = GroundDetector.SlopeCompensation(impulse, Body.position);
            Body.AddForce(impulse, ForceMode.Acceleration);
            return impulse;
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyLimitVelocity()
        {
            //note that we only take horizonal movement into account so that gravity doesn't get negated
            switch (SpeedLimitMethod)
            {
                case MaxSpeedLimitMethod.DirectVelocitySet:
                    {
                        if (MoveMode == MovementMode.Flying)
                        {
                            Vector3 vel = Body.velocity;

                            if (Mathf.Abs(vel.y) > MaxFlySpeed)
                            {
                                float flyDir = 0;
                                if (vel.y > 0) flyDir = 1;
                                else if (vel.y < 0) flyDir = -1;

                                Body.velocity = new Vector3(Body.velocity.x, flyDir * MaxFlySpeed, Body.velocity.z);
                            }
                        }

                        if (MovementVelocity.sqrMagnitude > EffectiveMaxSpeed * EffectiveMaxSpeed)
                            Body.velocity = (MovementVelocity.normalized * EffectiveMaxSpeed) + GravityVelocity;
                        break;
                    }
                case MaxSpeedLimitMethod.SubtractiveVelocity:
                    {
                        float currSpeed = MovementVelocity.sqrMagnitude;
                        if (currSpeed > EffectiveMaxSpeed * EffectiveMaxSpeed)
                        {
                            var brakeFac = currSpeed - EffectiveMaxSpeed;
                            var brakeVec = MovementVelocity.normalized * brakeFac;
                            Body.AddForce(-brakeVec);
                            return;
                        }
                        break;
                    }
                case MaxSpeedLimitMethod.CompoundSubtractiveVelocity:
                    {
                        float currSpeed = MovementVelocity.sqrMagnitude;
                        if (currSpeed > EffectiveMaxSpeed * EffectiveMaxSpeed)
                        {
                            var brakeFac = currSpeed - EffectiveMaxSpeed;
                            var brakeVec = MovementVelocity.normalized * brakeFac;
                            Body.AddForce(-brakeVec * BrakingForce);
                            return;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyDampening()
        {
            if (MoveMode == MovementMode.Walking)
            {
                if (Dampening > 0 && GroundDetector.IsGrounded)
                    Body.AddForce(MovementVelocity * -Dampening, ForceMode.Acceleration); //do not dampen vertical movement or gravity will suck
            }
            else
            {
                if (FlyingDampening > 0)
                    Body.AddForce(Body.velocity * -FlyingDampening, ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyGravity()
        {
            if (!GravityEnabled) return;
            if (MoveMode == MovementMode.Flying) return;

            Vector3 grav = Physics.gravity;

            //if we are grounded and not jumping, we want to adjust the direction of gravity to match the floor we are standing on.
            //this way we can avoid the usual issues of sliding down hills due to gravity or when jumping. it also corrects the
            //for the difference in player intended movement and the final movement results due to gravity sliding them downhill slightly
            if (GravityMatchesFloor && GroundDetector.IsGrounded && !GroundDetector.IsOnSlope)
            {
                //gravity pulls us to the surface of the slope
                if (!JumpGravityLatch)
                {
                    //BUG FIX 2/26/2023
                    //so there is a problem here where we can grab the supposed floor normal
                    //and it's actually completely horizontal due to touching a wall.
                    //Since we are technically counted as grounded AND touching an actual floor
                    //this still passes all of the above tests. We'll need an extra check here to make sure we
                    //really don't have a 'floor normal' that is beyond the angle of the allowed max slope
                    float angle = Vector3.Angle(Vector3.up, GroundDetector.FloorNormal);
                    if(angle < GroundDetector.GroundSlopeLimit)
                        grav = -GroundDetector.FloorNormal * Physics.gravity.magnitude;
                }

                //Debug.DrawRay(transform.position, grav, Color.yellow);
            }
            else JumpGravityLatch = false;

            Body.AddForce(grav * GravityScale, ForceMode.Acceleration);
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyJump()
        {
            if ((Time.time - JumpPressTimer < JumpFudgeTime) &&
                !GroundDetector.IsOnSlope &&
                (GroundDetector.HasBeenGrounded || JumpInc < AirJumps))
            {
                JumpedLastFrame = true;
                Jumping = true;
                JumpInc++;
                JumpPressTimer = 0;                  //ensures we don't count ourselves as having pressed jump recently
                GroundDetector.ResetGroundedTimer(); //ensures we don't count ourselves as having been grounded recently
                                                     //set the velocity directly so that moving up or down slopes does not affect our jump height
                Body.velocity = new Vector3(Body.velocity.x, JumpForce, Body.velocity.z);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        void ApplyFly()
        {
            var impulse = FlyAcceleration * JumpPressTimer * Vector3.up;
            Body.AddForce(impulse, ForceMode.Acceleration);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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
            if (GroundDetector.IsGrounded) JumpInc = 0;
            if (Jumping && GroundDetector.IsGrounded)
            {
                if (JumpedLastFrame)
                    JumpedLastFrame = false;
                else Jumping = false;
            }

            ApplyPhysicsMaterials();
            ApplyMovement();
            if (MoveMode == MovementMode.Flying) ApplyFly();
            ApplyLimitVelocity();
            ApplyDampening();
            ApplyGravity();
            if (MoveMode == MovementMode.Walking)
            {
                ApplyJump();
                if (GlueToFloor)
                {
                    StepsSinceLastGrounded++;
                    if (!Jumping && (GroundDetector.IsGrounded || GluedToFloor()))
                    {
                        StepsSinceLastGrounded = 0;
                    }
                }
            }

        }

    }
}


