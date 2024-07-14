using Unity.Collections;
using Unity.Transforms;
using Unity.Entities;
using Unity.Physics;
using Unity.Burst;
using Unity.Jobs;

public struct AboveRoofTag : IComponentData { }
public struct FreeNucleotideTag : IComponentData { }
public struct LeftBackbone : IComponentData {
    public Entity jointEntity;
}

public partial struct ReplicationAutocompleteChainTopSystem : ISystem {
    private EntityQuery freeNucleotidesQuery;
    private EntityQuery backboneQuery;
    public const float RoofZ = 7f;

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<BelongsToDishTag>();
        freeNucleotidesQuery = SystemAPI.QueryBuilder().WithAll<FreeNucleotideTag, ActorType>().Build();
        backboneQuery = SystemAPI.QueryBuilder().WithAll<Backbone, PhysicsVelocity>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new AutocompleteChainTopJob {
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            prefabs = SystemAPI.GetSingleton<PrefabLibrary>(),
            typeLookup = SystemAPI.GetComponentLookup<ActorType>(true),
            socketLookup = SystemAPI.GetComponentLookup<ConnectableSocket>(false),
            connectableLookup = SystemAPI.GetComponentLookup<ConnectableEntity>(false),
            freeEntities = freeNucleotidesQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var entHandle).AsDeferredJobArray(),
            freeTypes = freeNucleotidesQuery.ToComponentDataListAsync<ActorType>(state.WorldUpdateAllocator, state.Dependency, out var componentHandle)
                .AsDeferredJobArray(),
            finalLookup = SystemAPI.GetComponentLookup<FinalBackboneTag>(true),
            velocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(false),
            backboneEntities = backboneQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var backboneHandle).AsDeferredJobArray(),
        }.Schedule(JobHandle.CombineDependencies(state.Dependency, JobHandle.CombineDependencies(backboneHandle, entHandle, componentHandle)));
    }

    [WithAll(typeof(ConnectableSocket))]
    [WithNone(typeof(AboveRoofTag))]
    [WithAll(typeof(BelongsToReplicationTag))]
    [BurstCompile]
    private partial struct AutocompleteChainTopJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;
        [ReadOnly] public PrefabLibrary prefabs;
        [ReadOnly] public ComponentLookup<ActorType> typeLookup;
        public ComponentLookup<ConnectableSocket> socketLookup;
        public ComponentLookup<ConnectableEntity> connectableLookup;
        [ReadOnly] public NativeArray<ActorType> freeTypes;
        [ReadOnly] public NativeArray<Entity> freeEntities;
        [ReadOnly] public ComponentLookup<FinalBackboneTag> finalLookup;
        [WriteOnly] public ComponentLookup<PhysicsVelocity> velocityLookup;
        [ReadOnly] public NativeArray<Entity> backboneEntities;

        public void Execute(Entity ent, in LocalTransform transform) {
            if (transform.Position.z < RoofZ) { return; }

            ecb.AddComponent<AboveRoofTag>(ent);

            // complement if it's single nucleotide
            if (typeLookup.HasComponent(ent)) {
                if (connectableLookup[ent].connectedTo != Entity.Null                    //not free
                    && !typeLookup.HasComponent(connectableLookup[ent].connectedTo)      //connected to backbone
                    && socketLookup[ent].connectedEntity == Entity.Null                  //no complementary nucleotide
                ) {
                    // find from free nucleotides and connect it
                    var complementary = typeLookup[ent].value.Complement();
                    var selectedFree = Entity.Null;
                    for (int i = 0; i < freeEntities.Length; i++) {
                        if (freeTypes[i].value == complementary) {
                            selectedFree = freeEntities[i];
                            break;
                        }
                    }
                    if (selectedFree != Entity.Null) {
                        ecb.SetComponent(selectedFree, LocalTransform.FromPosition(transform.Position + new Unity.Mathematics.float3(-1f, 0, 0)));
                        ConnectableAttachOnContactSystem.ConnectEntities(selectedFree, ent, ref ecb, ref connectableLookup, ref socketLookup, in typeLookup, out _, false);
                    }
                }
            } else {                                                                    //backbone
                if (finalLookup.HasComponent(ent)) {
                    //stop all backbone from moving
                    for (int i = 0; i < backboneEntities.Length; i++) {
                        velocityLookup.GetRefRW(backboneEntities[i]).ValueRW = default;
                        
                    }
                }
            }

            //TODO: trying to close chain with another backbone is too buggy
            //if (typeLookup.HasComponent(connectableLookup[ent].connectedTo)) {
            //    var backboneEnt = ecb.Instantiate(prefabs.replicationBackboneLeft);
            //    ecb.AddComponent<ConnectableEntity>(backboneEnt);
            //    ecb.RemoveComponent<ConnectableSocket>(backboneEnt);
            //    ecb.SetComponent(backboneEnt, LocalTransform.FromPosition(transform.Position + new float3(-1, 0, 0)));
            //    ConnectableAttachOnContactSystem.ConnectEntities(backboneEnt, ent, ref ecb, ref connectableLookup, ref socketLookup, in typeLookup, out var jointEnt, true, true);
            //    ecb.AddComponent<LeftBackbone>(backboneEnt, new() {
            //        jointEntity = jointEnt,
            //    });

            //    //LimitDOF
            //    var limitJointEnt = ecb.CreateEntity();
            //    var selfPair = new PhysicsConstrainedBodyPair(
            //        backboneEnt,
            //        Entity.Null,
            //        false
            //    );
            //    ecb.AddSharedComponent<PhysicsWorldIndex>(limitJointEnt, new());
            //    var limitJoint = PhysicsJoint.CreateLimitedDOF(
            //            new RigidTransform() { pos = transform.Position + new float3(-1, 0, 0), rot = quaternion.identity },
            //            new bool3(true, true, false),
            //            new bool3(true, false, true));
            //    ecb.AddComponent(limitJointEnt, selfPair);
            //    ecb.AddComponent(limitJointEnt, limitJoint);
            //}
        }
    }
}
