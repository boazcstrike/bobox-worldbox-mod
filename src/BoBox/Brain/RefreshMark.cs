namespace BoBox.Brain
{
    /// <summary>When a city was last tried by <see cref="ScenarioCache"/> and its zone count then.</summary>
    internal readonly struct RefreshMark
    {
        public readonly double At;
        public readonly int Zones;

        public RefreshMark(double at, int zones)
        {
            At = at;
            Zones = zones;
        }
    }
}
