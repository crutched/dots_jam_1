using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;

public partial struct DishOrganismLifetimeSystem : ISystem {
    private Random rand;

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<Lifetime>();
        rand = Random.CreateFromIndex(1232345);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new LifetimeJob {
            deltaTime = SystemAPI.Time.DeltaTime,
            prefabs = SystemAPI.GetSingleton<PrefabLibrary>(),
            rand = Random.CreateFromIndex(rand.NextUInt()),
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
        }.Schedule(state.Dependency);
    }

    [WithNone(typeof(OrganismInReplicationStateTag))]
    [WithDisabled(typeof(Consumed))]
    [BurstCompile]
    private partial struct LifetimeJob : IJobEntity {
        [ReadOnly] public float deltaTime;
        [WriteOnly] public EntityCommandBuffer ecb;
        [ReadOnly] public PrefabLibrary prefabs;

        public Random rand;

        public void Execute(
                EnabledRefRW<Consumed> consumedRW,
                ref Lifetime lifetime,
                in NucleotideStorage storage,
                in DynamicBuffer<DNANucleotide> dna,
                in LocalToWorld transform
        ) {
            lifetime.timeLeft -= deltaTime;

            if (lifetime.timeLeft > 0) {
                return;
            }

            consumedRW.ValueRW = true;

            for (int i = 0; i < storage.Count(); i++) {
                SpawnNucleotide(ref ecb, in prefabs, (byte)(1 << rand.NextInt(4)), rand.NextInt(100) < 5, out var nucleotideEnt);
                ecb.AddComponent(nucleotideEnt, transform);
            }
            var dnaArray = dna.AsNativeArray();
            for (int i = 0; i < dnaArray.Length; i++) {
                SpawnNucleotide(ref ecb, in prefabs, dnaArray[i].value, rand.NextInt(100) < 5, out var nucleotideEnt);
                ecb.AddComponent(nucleotideEnt, transform);
            }
        }
    }

    private static void SpawnNucleotide(ref EntityCommandBuffer ecb, in PrefabLibrary prefabs, in byte typeOfNucleotide, bool noRender, out Entity nucleotideEnt) {
        nucleotideEnt = ecb.Instantiate(
            noRender ? prefabs.nucleotideNoRender :
                typeOfNucleotide == ActorType.Adenine ? prefabs.adenine :
                    typeOfNucleotide == ActorType.Cytosine ? prefabs.cytosine :
                        typeOfNucleotide == ActorType.Guanine ? prefabs.guanine :
                            prefabs.thymine
            );
        if (noRender) {
            ecb.SetComponent<ActorType>(nucleotideEnt, new() {
                value = typeOfNucleotide,
            });
        }
        ecb.RemoveComponent<LinkedEntityGroup>(nucleotideEnt);
    }
}
