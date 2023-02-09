using Toolbox.GCCI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Toolbox.CharacterController
{
    [RequireComponent(typeof(Rigidbody))]
    public class CameraAim : MonoBehaviour, IAimer
    {
        public Transform PitchTrans;
        public float MinPitch = -90;
        public float MaxPitch = 90;
        public float MinYaw = -360;
        public float MaxYaw = 360;
        public float SensitivityPitch = 1;
        public float SensitivityYaw = 1;

        float Pitch;
        float Yaw;
        Rigidbody Body;

        public void Awake()
        {
            Body = GetComponent<Rigidbody>();
        }

        public bool AimEnabled { get; set; } = true;

        public float MoveX { set { } }
        public float MoveY { set { } }

        public float AimX
        {
            set
            {
                Yaw = value * SensitivityYaw;
                Yaw = Mathf.Clamp(Yaw, MinYaw, MaxYaw);

                PitchTrans.localRotation = Quaternion.AngleAxis(Pitch, -Vector3.right);
                Body.rotation *= Quaternion.AngleAxis(Yaw, Vector3.up);
            }
        }

        public float AimY
        {
            set
            {
                Pitch += value * SensitivityPitch;
                Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);

                PitchTrans.localRotation = Quaternion.AngleAxis(Pitch, -Vector3.right);
                Body.rotation *= Quaternion.AngleAxis(Yaw, Vector3.up);
            }
        }

        /// <summary>
        /// Processes direct mouse input as accelerations in x and y axies.
        /// </summary>
        /// <param name="inputAccel"></param>
        public void Aim(Vector2 inputAccel)
        {
            if (!AimEnabled) return;


            Pitch += inputAccel.y * SensitivityPitch;
            Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);

            Yaw = inputAccel.x * SensitivityYaw;
            Yaw = Mathf.Clamp(Yaw, MinYaw, MaxYaw);

            PitchTrans.localRotation = Quaternion.AngleAxis(Pitch, -Vector3.right);
            Body.rotation *= Quaternion.AngleAxis(Yaw, Vector3.up);
        }

        /// <summary>
        /// Points the camera at a specfic world-space target.
        /// </summary>
        /// <param name="target"></param>
        public void Aim(Vector3 targetPos)
        {
            Body.rotation = Quaternion.LookRotation(targetPos - Body.position, Vector3.up);
        }

        /// <summary>
        /// Handler for the new Unity InputSystem
        /// </summary>
        /// <param name="value"></param>
        public void OnLook(InputValue value)
        {
            var accel = value.Get<Vector2>();
            Aim(accel);
        }
    }
}