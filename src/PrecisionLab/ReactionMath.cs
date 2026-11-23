using System;

namespace PrecisionLab
{
    /// <summary>
    /// Shared per-second conversion maths for the mod's chemical chains.
    ///
    /// The vanilla chemistry tick runs once per second, so a 2%-per-tick curve made big batches
    /// feel extremely slow (a 20 L batch took ~10 minutes of real time). We now convert 20% of the
    /// limiting reagent per tick and flush the last 100 µL in one go, which keeps small doses
    /// instant while finishing a full production batch in roughly 15-20 seconds of heating.
    /// </summary>
    internal static class ReactionMath
    {
        internal const int FinalFlush = 100;

        internal static int Step(int limiting)
        {
            if (limiting <= 0)
            {
                return 0;
            }
            if (limiting <= FinalFlush)
            {
                return limiting;
            }
            int amount = Math.Max(1, limiting / 5);
            return Math.Min(Math.Min(amount, 20000), limiting);
        }
    }
}
