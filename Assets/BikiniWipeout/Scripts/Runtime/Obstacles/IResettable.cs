namespace BikiniWipeout
{
    /// <summary>Implemented by obstacles that can return to their starting state on demand.</summary>
    public interface IResettable
    {
        /// <summary>Returns the obstacle to exactly the state it had when the course started.</summary>
        void ResetState();
    }
}
