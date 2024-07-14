using Unity.Collections;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;
using Unity.Jobs;
using System.Data;

public struct ReplicationTerminationTag : IComponentData { }
public struct SpawnNewOrganismTag : IComponentData { }
public struct SelectNewOrganismTag : IComponentData { }

public partial struct ReplicationTerminationSystem : ISystem {
    private EntityQuery backboneQuery;
    private EntityQuery ternimationQuery;
    private EntityQuery replicationRelatedQuery;

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        ternimationQuery = SystemAPI.QueryBuilder().WithAll<ReplicationTerminationTag>().Build();
        replicationRelatedQuery = SystemAPI.QueryBuilder().WithAll<BelongsToReplicationTag>().Build();
        state.RequireAnyForUpdate(new NativeArray<EntityQuery>(2, Allocator.Temp) {
            [0] = ternimationQuery,
            [1] = replicationRelatedQuery
        });
        backboneQuery = SystemAPI.QueryBuilder().WithAll<Backbone, LocalTransform>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new ReplicationTerminateJob {
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            finalBackbone = SystemAPI.GetSingletonEntity<FinalBackboneTag>(),
            backboneLookup = SystemAPI.GetComponentLookup<Backbone>(true),
            typeLookup = SystemAPI.GetComponentLookup<ActorType>(true),
            socketLookup = SystemAPI.GetComponentLookup<ConnectableSocket>(true),
            ltLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            aboveRoofLookup = SystemAPI.GetComponentLookup<AboveRoofTag>(true),
            backboneEntities = backboneQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var backboneHandle).AsDeferredJobArray(),
        }.Schedule(JobHandle.CombineDependencies(state.Dependency, backboneHandle));

        if (ternimationQuery.IsEmptyIgnoreFilter && SystemAPI.QueryBuilder().WithAll<OrganismInReplicationStateTag>().Build().IsEmpty) {
            state.Dependency = new CleanReplicationField {
                ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(state.Dependency);
        }
    }

    [WithAll(typeof(ReplicationTerminationTag))]
    [BurstCompile]
    private partial struct ReplicationTerminateJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;
        [ReadOnly] public Entity finalBackbone;
        [ReadOnly] public ComponentLookup<Backbone> backboneLookup;
        [ReadOnly] public ComponentLookup<ActorType> typeLookup;
        [ReadOnly] public ComponentLookup<ConnectableSocket> socketLookup;
        [ReadOnly] public ComponentLookup<AboveRoofTag> aboveRoofLookup;
        public ComponentLookup<LocalTransform> ltLookup;
        [ReadOnly] public NativeArray<Entity> backboneEntities;

        public void Execute(Entity ent, in DynamicBuffer<DNANucleotide> originalDNA, in Generation generation, in LocalToWorld transform) {
            if (!aboveRoofLookup.HasComponent(finalBackbone)) {
                //push all chain to top a little
                for (int i = 0; i < backboneEntities.Length; i++) {
                    var ltRW = ltLookup.GetRefRW(backboneEntities[i]);
                    ltRW.ValueRW = ltRW.ValueRW.Translate(new Unity.Mathematics.float3(0, 0, 0.05f));
                }
                return;
            }
            ecb.RemoveComponent<ReplicationTerminationTag>(ent);
            ecb.RemoveComponent<OrganismInReplicationStateTag>(ent);
            var newDNA = new NativeList<DNANucleotide>(originalDNA.Length, Allocator.Temp);
            var currentEnt = finalBackbone;
            while (currentEnt != Entity.Null) {
                if (socketLookup.TryGetComponent(currentEnt, out var backboneSocket)
                        && typeLookup.TryGetComponent(backboneSocket.connectedEntity, out var firstNucleotideType)
                        && socketLookup.TryGetComponent(backboneSocket.connectedEntity, out var nucleotideSocket)
                        && nucleotideSocket.connectedEntity != Entity.Null
                ) {
                    newDNA.Add(new() {
                        value = firstNucleotideType.value,
                    });
                }
                if (backboneLookup.TryGetComponent(currentEnt, out var currentBackbone)) {
                    currentEnt = currentBackbone.prev;
                } else {
                    currentEnt = Entity.Null;
                }
            }
            var newOrganism = ecb.CreateEntity();
            ecb.AddComponent<SpawnNewOrganismTag>(newOrganism);
            ecb.AddBuffer<DNANucleotide>(newOrganism);
            ecb.AddComponent(newOrganism, generation);
            ecb.AddComponent(newOrganism, transform);
            ecb.AddComponent<SelectNewOrganismTag>(newOrganism);
            for (int i = newDNA.Length - 1; i >= 0; i--) {
                ecb.AppendToBuffer(newOrganism, newDNA[i]);
            }
        }
    }

    [WithAll(typeof(BelongsToReplicationTag))]
    [BurstCompile]
    private partial struct CleanReplicationField : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;

        public void Execute(Entity ent) {
            ecb.DestroyEntity(ent);
        }
    }
}
