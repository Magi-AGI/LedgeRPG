namespace LedgeRPG.Core.World
{
    /// Termination outcome of an episode. Ordered by priority: if two conditions
    /// are simultaneously satisfied on the same step (e.g. last food consumed
    /// on the final step of the step limit), the higher-priority reason wins.
    /// World.CheckTermination enforces this ordering explicitly.
    public enum TerminalReason
    {
        None = 0,
        TargetReached = 1,   // all food consumed — success
        EnergyDepleted = 2,  // energy <= 0 — failure
        StepLimit = 3        // step count hit limit without success — failure
    }
}
