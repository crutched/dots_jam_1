using Unity.Collections;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;

public partial struct DishSpawnSpawnOrganismSystem : ISystem {

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<SpawnNewOrganismTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new SpawnOrganismJob {
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            prefabs = SystemAPI.GetSingleton<PrefabLibrary>(),
            selectLookup = SystemAPI.GetComponentLookup<SelectNewOrganismTag>(true),
        }.Schedule(state.Dependency);
    }

    [WithAll(typeof(SpawnNewOrganismTag))]
    [BurstCompile]
    private partial struct SpawnOrganismJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;
        [ReadOnly] public PrefabLibrary prefabs;
        [ReadOnly] public ComponentLookup<SelectNewOrganismTag> selectLookup;

        public void Execute(Entity ent, in DynamicBuffer<DNANucleotide> dna, in LocalToWorld transform, in Generation generation) {
            var organismEnt = ecb.Instantiate(prefabs.organism);
            ecb.SetComponent(organismEnt, transform);
            var dnaArray = dna.AsNativeArray();
            for (int i = 0; i < dnaArray.Length; i++) {
                ecb.AppendToBuffer(organismEnt, dnaArray[i]);
            }
            ecb.SetComponent<Generation>(organismEnt, new() {
                value = (ushort)(generation.value + 1),
            });

            if (selectLookup.HasComponent(ent)) {
                ecb.AddComponent<SelectNewOrganismTag>(organismEnt);
            }

            ecb.DestroyEntity(ent);
        }
    }
}
