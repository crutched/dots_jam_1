using Unity.Collections;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;

public partial struct DishAutoreplicateOrganismSystem : ISystem {

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<AutoreplicateTime>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new AutoreplicateJob {
            deltaTime = SystemAPI.Time.DeltaTime,
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
        }.Schedule(state.Dependency);
    }

    [WithNone(typeof(OrganismInReplicationStateTag))]
    [BurstCompile]
    private partial struct AutoreplicateJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;
        [ReadOnly] public float deltaTime;

        public void Execute(
                    ref AutoreplicateTime countdown,
                    ref NucleotideStorage storage,
                    in LocalToWorld transform,
                    in DynamicBuffer<DNANucleotide> dna,
                    in Generation generation
        ) {
            countdown.timeLeft -= deltaTime;

            if (countdown.timeLeft > 0) { return; }
            if (!storage.IsFull()) { return; }

            var request = ecb.CreateEntity();
            ecb.AddComponent<SpawnNewOrganismTag>(request);
            ecb.AddComponent(request, transform);
            ecb.AddBuffer<DNANucleotide>(request);
            ecb.AddComponent(request, generation);
            var dnaArray = dna.AsNativeArray();
            for (int i = 0; i < dnaArray.Length; i++) {
                ecb.AppendToBuffer(request, dnaArray[i]);
                storage.Remove(dnaArray[i].value);
                storage.Remove(dnaArray[i].value.Complement());
            }

            countdown.timeLeft = countdown.modifierMultiplier * AutoreplicateTime.DefaultReplicationTime;
        }
    }
}
