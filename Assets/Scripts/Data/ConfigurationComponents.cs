using Unity.Entities;

public struct SimulationRate : IComponentData {
    public bool UseFixedRate;
    public float FixedTimeStep;

    public float normalTimeStep;
    public float TimeScale;

    public float UnscaledDeltaTime;

    public bool Update;
}

public struct TimeStepModification : IComponentData {
    public float scale;
}
