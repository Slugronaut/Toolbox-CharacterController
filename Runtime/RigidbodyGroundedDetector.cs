using Peg.GCCI;
using UnityEngine;
using UnityEngine.Events;

namespace Peg.CharacterController
{
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyGroundedDetector : MonoBehaviour, IGroundedState
    {
        #region Public Fields
        [Tooltip("The max angle of the ground before it is considered a slope and not a floor.")]
        [Range(0, 90)]
        public float GroundSlopeLimit = 65f;
        [Tooltip("How long can we go without the CharacterControler reporting a grounded state before we are truely considered to no longer be grounded")]
        public float GroundedDelayTime = 0.2f;
        [Tooltip("An extra scaling factor to the grounded delay time for triggering the OnLandedEvent. Helps prevent triggering when walking over bumpy terrain at high speeds.")]
        public float GroundedEventDelayScale = 1.0f;
        [Tooltip("When airborn, the OnStopSlidingEvent is triggered if.")]
        public bool AirbornEndsSlide = true;
        public float MinYVelForSlide = -3;
        #endregion


        public UnityEvent<IGroundedState> OnStartSlidingEvent;
        public UnityEvent<IGroundedState> OnStopSlidingEvent;
        public UnityEvent<IGroundedState> OnLandedEvent;


        #region Private Fields
        Rigidbody Body;
        Collider Floor;
        Collider Slope;
        readonly ContactPoint[] Contacts = new ContactPoint[6];
        #endregion


        #region Properties
        public bool IsGrounded => Floor != null || Slope != null;
        public bool HasBeenGrounded => IsGrounded || (Time.timeAsDouble - LastGroundedTime) < GroundedDelayTime;
        public bool IsOnSlope => Slope != null;
        public double LastGroundedTime { get; private set; }
        public Vector3 FloorNormal { get => LastContact.normal; }
        public ContactPoint LastContact { get; private set; }
        public float SlopeAngleDot { get; private set; }
        public bool GroundedEnabled { get; set; } = true;
        public bool IsFalling
        {
            get
            {
                return !HasBeenGrounded && GetComponent<Rigidbody>().velocity.y < 0;
            }
        }
        public bool IsSliding { get; private set; }
        #endregion

        private void Awake()
        {
            //precalculate the y-component of a vector that is the max allowed slope from vector3.up
            SlopeAngleDot = (Quaternion.AngleAxis(GroundSlopeLimit, Vector3.forward) * Vector3.up).y;
            Body = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionStay(Collision collision)
        {
            int count = collision.GetContacts(Contacts);
            for (int i = 0; i < Mathf.Min(count, Contacts.Length); i++)
            {
                LastContact = Contacts[i];
                float angle = Vector3.Angle(Vector3.up, FloorNormal);
                if (angle < 90)
                {
                    if (Slope == null && Floor == null && (Time.timeAsDouble - LastGroundedTime) > GroundedDelayTime * GroundedEventDelayScale)
                        OnLandedEvent.Invoke(this);

                    if (angle < GroundSlopeLimit)
                    {
                        IsSliding = false;
                        OnStopSlidingEvent.Invoke(this);
                        //floors always take priority, return now
                        Floor = collision.collider;
                        Slope = null;
                        LastGroundedTime = Time.timeAsDouble;
                        return;
                    }
                    else
                    {
                        //don't return, we want to check in case there are
                        ////any other contacts that would count as a floor
                        Floor = null;
                        Slope = collision.collider;
                        if (!IsSliding && Body.velocity.y < MinYVelForSlide)
                        {
                            IsSliding = true;
                            OnStartSlidingEvent.Invoke(this);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionExit(Collision collision)
        {
            if(collision.collider == Slope)
            {
                if ((!HasBeenGrounded && AirbornEndsSlide) ||
                    Body.velocity.y > MinYVelForSlide &&
                    IsSliding)
                {
                    IsSliding = false;
                    OnStopSlidingEvent.Invoke(this);
                }
                Floor = null;
                Slope = null;
                LastGroundedTime = Time.timeAsDouble;
            }
            else if(collision.collider == Floor)
            {
                Floor = null;
                Slope = null;
                LastGroundedTime = Time.timeAsDouble;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearGroundedState()
        {
            //Debug.Log("Floor: " + (Floor == null ? "null" : "set") + "  Slope: " + (Slope == null ? "null" : "set"));
            Floor = null;
            Slope = null;
            LastGroundedTime = -100;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ResetGroundedTimer()
        {
            LastGroundedTime = -100;
        }

        /// <summary>
        /// Adjusts a movement vector to match the slope
        /// </summary>
        /// <param name="moveImpulse"></param>
        /// <param name="antiHillClimb"></param>
        /// <returns></returns>
        public Vector3 SlopeCompensation(Vector3 moveImpulse, Vector3 pos)
        {
            if (HasBeenGrounded)
            {
                /*
                if(!IsGrounded)
                {
                    if(Physics.Raycast(pos+new Vector3(0,0.1f,0), Vector3.down, out RaycastHit hitInfo, 1))
                    {
                        FloorNormal = hitInfo.normal;
                    }
                }
                */
                var slopedImpulse = moveImpulse + Vector3.ProjectOnPlane(moveImpulse, FloorNormal);
                if (slopedImpulse.y < 0) return slopedImpulse;
            }
            return moveImpulse;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="moveImpulse"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Vector3 CalculateSlopeCounterClimbForce(Vector3 moveImpulse)
        {
            const float SlopeCounterForce = 0.075f;
            if (HasBeenGrounded)
            {
                var slopeDown = Vector3.ProjectOnPlane(Vector3.down, FloorNormal).normalized;
                //check for being on a steep slope, if true, we need to counteract motion in the direction of the slope.
                float frac = Vector3.Dot(moveImpulse, -slopeDown);
                if (frac > 0)
                    return frac * moveImpulse.magnitude * SlopeCounterForce * slopeDown;
            }

            return Vector3.zero;
        }
    }
}