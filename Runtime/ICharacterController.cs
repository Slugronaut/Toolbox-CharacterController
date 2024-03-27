
using UnityEngine;

namespace Peg.CharacterController
{
    public interface ICharacterController
    {

        public enum MaxSpeedLimitMethod
        {
            None,
            DirectVelocitySet,
            SubtractiveVelocity,
            CompoundSubtractiveVelocity,
        }

        public enum MovementMode
        {
            Walking,
            Flying,
        }

        Rigidbody RigidbodyComp { get; }
        int AddSpeedBonus(float bonus);
        int AddSpeedPenalty(float penalty);
        void RemoveSpeedBonus(int handle);
        void RemoveSpeedPenalty(int handle);
        MovementMode MoveMode { get; set; }
        MaxSpeedLimitMethod SpeedLimitMethod { get; }
    }
}
