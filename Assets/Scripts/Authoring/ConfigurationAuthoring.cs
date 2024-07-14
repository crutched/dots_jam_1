using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class ConfigurationAuthoring : MonoBehaviour {
    public bool UseFixedSimulationDeltaTime;
    public float FixedDeltaTime;

    public class Baker : Baker<ConfigurationAuthoring> {

        public override void Bake(ConfigurationAuthoring authoring) {
            var ent = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(ent, new SimulationRate {
                UseFixedRate = authoring.UseFixedSimulationDeltaTime,
                FixedTimeStep = authoring.FixedDeltaTime,
                normalTimeStep = authoring.FixedDeltaTime,
                TimeScale = 0f,
                Update = true,
            });
        }
    }
}
