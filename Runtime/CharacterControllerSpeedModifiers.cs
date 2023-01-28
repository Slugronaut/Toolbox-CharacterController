using UnityEngine;
using UnityEngine.InputSystem;
using static Toolbox.CharacterController.ICharacterController;

namespace Toolbox.CharacterController
{
    [RequireComponent(typeof(RigidbodyGroundedDetector))]
    [RequireComponent(typeof(ICharacterController))]
    public class CharacterControllerSpeedModifiers : MonoBehaviour
    {
        enum CrouchStates
        {
            Standing,
            Crouching,
        }

        public float SprintBonus = 0.5f;
        public float WalkPenalty = 0.5f;
        public float CrouchPenalty = 0.34f;
        public float CrouchHeightShrink = -0.5f;
        public float CrouchRate = 10;
        public Transform CameraTrans;
        public CapsuleCollider Collider;

        bool SprintPressed;
        int SprintBonusHandle = -1;
        int WalkPenaltyHandle = -1;
        int CrouchPenaltyHandle = -1;
        ICharacterController Controller;
        RigidbodyGroundedDetector GroundDetector;
        float DefaultHeight;
        float DefaultHeightOffset;
        float DefaultCameraPos;
        float CrouchTransition = 0;
        CrouchStates CrouchState = CrouchStates.Standing;

        private void Awake()
        {
            Controller = GetComponent<ICharacterController>();
            GroundDetector = GetComponent<RigidbodyGroundedDetector>();

            DefaultHeight = Collider.height;
            DefaultHeightOffset = Collider.center.y;
            DefaultCameraPos = CameraTrans.localPosition.y;
        }

        public void OnSprint(InputValue value)
        {
            SprintPressed = value.isPressed;
        }

        public void StartSprint()
        {
            if (SprintBonusHandle == -1 && Controller.MoveMode == MovementMode.Walking)
            {
                EndWalk();
                EndCrouch();
                SprintBonusHandle = Controller.AddSpeedBonus(SprintBonus);
            }
        }

        public void EndSprint()
        {
            if (SprintBonusHandle != -1)
            {
                Controller.RemoveSpeedBonus(SprintBonusHandle);
                SprintBonusHandle = -1;
            }
        }

        public void OnWalk(InputValue value)
        {
            //walking is a toggle, hence the reason we don't need to track it's state in Update()
            if (value.isPressed && SprintBonusHandle == -1)
            {
                if (WalkPenaltyHandle == -1)
                    StartWalk();
                else EndWalk();
            }
        }

        public void StartWalk()
        {
            if (WalkPenaltyHandle == -1)
                WalkPenaltyHandle = Controller.AddSpeedPenalty(WalkPenalty);
        }

        public void EndWalk()
        {
            if (WalkPenaltyHandle != -1)
            {
                Controller.RemoveSpeedPenalty(WalkPenaltyHandle);
                WalkPenaltyHandle = -1;
            }
        }

        public void OnCrouch(InputValue value)
        {
            if (value.isPressed && SprintBonusHandle == -1)
            {
                if (CrouchPenaltyHandle == -1)
                    StartCrouch();
                else EndCrouch();
            }
        }

        void StartCrouch()
        {
            if (CrouchPenaltyHandle == -1 && Controller.MoveMode == MovementMode.Walking)
            {
                CrouchPenaltyHandle = Controller.AddSpeedPenalty(CrouchPenalty);
                CrouchTransition = 0;
                CrouchState = CrouchStates.Crouching;
            }
        }

        void EndCrouch()
        {
            if (CrouchPenaltyHandle != -1)
            {
                Controller.RemoveSpeedPenalty(CrouchPenaltyHandle);
                CrouchPenaltyHandle = -1;
                CrouchTransition = 0;
                CrouchState = CrouchStates.Standing;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void Update()
        {
            if (Controller.MoveMode != MovementMode.Walking)
            {
                EndSprint();
                EndCrouch();
            }

            if (SprintPressed)
            {
                if (GroundDetector.IsGrounded && !GroundDetector.IsOnSlope)
                    StartSprint();
            }
            else
            {
                if (GroundDetector.IsGrounded)
                    EndSprint();
            }

            if (CrouchTransition != 1)
            {
                CrouchTransition += CrouchRate * Time.deltaTime;
                if (CrouchTransition > 1)
                    CrouchTransition = 1;
                float adjust;

                if (CrouchState == CrouchStates.Standing)
                    adjust = (CrouchHeightShrink * (1 - CrouchTransition));
                else adjust = (CrouchHeightShrink * CrouchTransition);

                CameraTrans.localPosition = new Vector3(0, DefaultCameraPos + adjust, 0);
                Collider.height = DefaultHeight + adjust;
                Collider.center = new Vector3(Collider.center.x, DefaultHeightOffset + adjust * 0.5f, Collider.center.z);
            }
        }
    }
}
