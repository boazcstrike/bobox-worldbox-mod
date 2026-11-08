namespace BoBox.Planner.Scenario
{
    /// <summary>
    /// The 7 city scenarios. A city can carry several at once. Coastal also covers a small island
    /// or a city that owns most of its island: the ledger treats "coastal or island" as one scenario.
    /// In the rules file and the API these are snake_case strings (forest_rich, crowded_frontier, ...).
    /// </summary>
    public enum ScenarioTag
    {
        ForestRich,
        FertilePlains,
        Rocky,
        Scarce,
        Coastal,
        CrowdedFrontier,
        OpenFrontier,
    }
}
