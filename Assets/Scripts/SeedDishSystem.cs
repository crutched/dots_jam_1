using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;

public struct BelongsToDishTag : IComponentData { }

[UpdateInGroup(typeof(MainThreadWorkGroup))]
public partial struct SeedDishSystem : ISystem {

    public struct DishNotInitialized : IComponentData { }

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<PrefabLibrary>();
        state.RequireForUpdate<DishNotInitialized>();
        state.RequireForUpdate<Dish>();

        var bootstrapEnt = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent<DishNotInitialized>(bootstrapEnt);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        //TODO: cleanup or create once only
        if (!SystemAPI.HasSingleton<SpatialDatabaseSingleton>()) {
            Entity runtimeResourcesEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(runtimeResourcesEntity, new SpatialDatabaseSingleton());
            var prefabLibrary = SystemAPI.GetSingleton<PrefabLibrary>();
            CreateTargetablesSpatialDatabase(ref state, in prefabLibrary, SystemAPI.GetSingleton<Dish>().radius * 1.2f);
        }

        state.Dependency = new DishSeedJob {
            rand = Random.CreateFromIndex(77777),
            dish = SystemAPI.GetSingleton<Dish>(),
            prefabs = SystemAPI.GetSingleton<PrefabLibrary>(),
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
        }.Schedule(state.Dependency);
    }

    private void CreateTargetablesSpatialDatabase(
            ref SystemState state,
            in PrefabLibrary prefabs,
            float simulationCubeHalfExtents
    ) {
        ref SpatialDatabaseSingleton spatialDatabaseSingleton = ref SystemAPI.GetSingletonRW<SpatialDatabaseSingleton>().ValueRW;
        spatialDatabaseSingleton.TargetablesSpatialDatabase =
            state.EntityManager.Instantiate(prefabs.spatialDatabase);
        SpatialDatabase spatialDatabase =
            state.EntityManager.GetComponentData<SpatialDatabase>(spatialDatabaseSingleton
                .TargetablesSpatialDatabase);
        DynamicBuffer<SpatialDatabaseCell> cellsBuffer =
            state.EntityManager.GetBuffer<SpatialDatabaseCell>(spatialDatabaseSingleton.TargetablesSpatialDatabase);
        DynamicBuffer<SpatialDatabaseElement> elementsBuffer =
            state.EntityManager.GetBuffer<SpatialDatabaseElement>(spatialDatabaseSingleton
                .TargetablesSpatialDatabase);

        SpatialDatabase.Initialize(
            simulationCubeHalfExtents,
            5,
            64,
            ref spatialDatabase,
            ref cellsBuffer,
            ref elementsBuffer);

        state.EntityManager.SetComponentData(spatialDatabaseSingleton.TargetablesSpatialDatabase, spatialDatabase);
    }

    [WithAll(typeof(DishNotInitialized))]
    [BurstCompile]
    private partial struct DishSeedJob : IJobEntity {
        [ReadOnly] public PrefabLibrary prefabs;
        [ReadOnly] public Dish dish;
        [WriteOnly] public EntityCommandBuffer ecb;

        public Random rand;

        public void Execute(Entity ent) {
            ecb.DestroyEntity(ent);

            for (int i = 0; i < dish.organisms; i++) {
                var request = ecb.CreateEntity();
                ecb.AddComponent<SpawnNewOrganismTag>(request);
                RandomPositionInDish(ref rand, in dish, out var pos);
                ecb.AddComponent<Generation>(request);

                ecb.AddComponent<LocalToWorld>(request, new() {
                    Value = LocalTransform.FromPosition(pos).ToMatrix(),
                });
                ecb.AddBuffer<DNANucleotide>(request);
                ecb.AppendToBuffer<DNANucleotide>(request, new() { value = ActorType.Adenine });
                ecb.AppendToBuffer<DNANucleotide>(request, new() { value = ActorType.Cytosine });
                ecb.AppendToBuffer<DNANucleotide>(request, new() { value = ActorType.Guanine });
                ecb.AppendToBuffer<DNANucleotide>(request, new() { value = ActorType.Thymine });
            }

            for (int i = 0; i < dish.nucleotides; i++) {
                RandomPositionInDish(ref rand, in dish, out var pos);

                var typeOfNucleotide = rand.NextInt(4);

                bool noRender = i % 20 != 0;

                var nucleotideEnt = ecb.Instantiate(
                    noRender ? prefabs.nucleotideNoRender :
                        typeOfNucleotide == 0 ? prefabs.adenine :
                            typeOfNucleotide == 1 ? prefabs.cytosine :
                                typeOfNucleotide == 2 ? prefabs.guanine :
                                    prefabs.thymine
                    );
                if (noRender) {
                    ecb.SetComponent<ActorType>(nucleotideEnt, new() {
                        value = (byte)(1 << typeOfNucleotide),
                    });
                }
                ecb.SetComponent<LocalToWorld>(nucleotideEnt, new() {
                    Value = LocalTransform.FromPosition(pos).ToMatrix(),
                });

                ecb.AddComponent<MoveDirection>(nucleotideEnt, new() {
                    value = rand.NextFloat3Direction(),
                });
                ecb.RemoveComponent<LinkedEntityGroup>(nucleotideEnt);
            }
        }
    }

    public static void RandomPositionInDish(ref Random rand, in Dish dish, out float3 pos) {
        var smallerRadius = dish.radius * 0.9f;
        var maxHeight = dish.height * 0.9f;
        var minHeight = dish.height * 0.1f;

        var r = smallerRadius * math.sqrt(rand.NextFloat());
        var theta = rand.NextFloat() * math.TAU;
        pos.x = r * math.cos(theta);
        pos.z = r * math.sin(theta);
        pos.y = rand.NextFloat(minHeight, maxHeight);
    }
}
