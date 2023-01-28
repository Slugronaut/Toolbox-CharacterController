
namespace Toolbox.CharacterController
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

        int AddSpeedBonus(float bonus);
        int AddSpeedPenalty(float penalty);
        void RemoveSpeedBonus(int handle);
        void RemoveSpeedPenalty(int handle);
        MovementMode MoveMode { get; }
        MaxSpeedLimitMethod SpeedLimitMethod { get; }
    }
}
