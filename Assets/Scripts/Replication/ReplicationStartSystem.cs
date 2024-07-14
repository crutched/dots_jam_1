using static Unity.Physics.Math;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;
using Unity.Physics;
using Unity.Burst;

public struct BelongsToReplicationTag : IComponentData { }
public struct FinalBackboneTag : IComponentData { }
public struct OrganismInReplicationStateTag : IComponentData { }

public partial struct ReplicationStartSystem : ISystem {

    public struct ReplicationInit : IComponentData { }

    private Random rand;

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<PrefabLibrary>();
        state.RequireForUpdate<ReplicationInit>();

        //var initEnt = state.EntityManager.CreateEntity();
        //state.EntityManager.AddComponent<ReplicationInit>(initEnt);
        rand = Random.CreateFromIndex(88888);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new ReplicationStartJob {
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            prefabs = SystemAPI.GetSingleton<PrefabLibrary>(),
            rand = Random.CreateFromIndex(rand.NextUInt()),
            typeLookup = SystemAPI.GetComponentLookup<ActorType>(true),
            socketLookup = SystemAPI.GetComponentLookup<ConnectableSocket>(false),
            connectableLookup = SystemAPI.GetComponentLookup<ConnectableEntity>(false),
        }.Schedule(state.Dependency);
    }

    [WithAll(typeof(ReplicationInit))]
    [BurstCompile]
    private partial struct ReplicationStartJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;
        [ReadOnly] public PrefabLibrary prefabs;
        public Random rand;
        [ReadOnly] public ComponentLookup<ActorType> typeLookup;
        public ComponentLookup<ConnectableSocket> socketLookup;
        public ComponentLookup<ConnectableEntity> connectableLookup;

        private const float RightBackboneX = 8.29f;
        private const float BackboneZOffset = -1.1f;
        private const float ReplicationY = -520f;

        public void Execute(Entity ent, ref NucleotideStorage storage, in DynamicBuffer<DNANucleotide> dna) {
            ecb.RemoveComponent<ReplicationInit>(ent);
            ecb.AddComponent<OrganismInReplicationStateTag>(ent);

            var dnaArray = dna.AsNativeArray();

            var prevBackbone = Entity.Null;
            var rootEnt = Entity.Null;
            var maxChainLenght = storage.Count() / 2;
            for (int i = 0; i < maxChainLenght; i++) {
                var backboneEnt = ecb.Instantiate(prefabs.replicationBackbone);
                if (i == 0) {
                    rootEnt = backboneEnt;
                }
                var pos = new float3(RightBackboneX, ReplicationY, 5f + i * BackboneZOffset);
                ecb.SetComponent(backboneEnt, LocalTransform.FromPosition(pos));

                //LimitDOF
                var limitJointEnt = ecb.CreateEntity();
                var selfPair = new PhysicsConstrainedBodyPair(
                    backboneEnt,
                    Entity.Null,
                    false
                );
                ecb.AddSharedComponent<PhysicsWorldIndex>(limitJointEnt, new());
                var limitJoint = PhysicsJoint.CreateLimitedDOF(
                        new RigidTransform() { pos = pos, rot = quaternion.identity },
                        new bool3(i == 0 || i == maxChainLenght - 1 || i % 10 == 0, true, false),
                        new bool3(true, i == 0 || i == maxChainLenght - 1 || i % 10 == 0, true));
                ecb.AddComponent(limitJointEnt, selfPair);
                ecb.AddComponent(limitJointEnt, limitJoint);
                if (i > 0) {
                    ecb.AddComponent<Backbone>(backboneEnt, new() { prev = prevBackbone });
                }

                //Connect backbone to prevOne
                if (i != 0) {
                    var pair = new PhysicsConstrainedBodyPair(
                        backboneEnt,
                        prevBackbone,
                        false
                    );
                    var jointEntity = ecb.CreateEntity();
                    ecb.AddSharedComponent<PhysicsWorldIndex>(jointEntity, new());

                    ecb.AddComponent(jointEntity, pair);
                    var physicsJoint = PhysicsJoint.CreateLimitedHinge(
                        new BodyFrame {
                            Axis = new float3(0, 0, 1),
                            PerpendicularAxis = new float3(1, 0, 0),
                            Position = new float3(0, 0, 0.5f)
                        },
                        new BodyFrame {
                            Axis = new float3(0, 0, 1),
                            PerpendicularAxis = new float3(1, 0, 0),
                            Position = new float3(0, 0, -0.6f)
                        },
                        math.radians(new FloatRange(-30, 30))
                    ); ;
                    physicsJoint.SetImpulseEventThresholdAllConstraints(new float3(math.INFINITY));
                    ecb.AddComponent(jointEntity, physicsJoint);
                }
                if (i == maxChainLenght - 1) {
                    ecb.AddComponent<FinalBackboneTag>(backboneEnt);
                }
                ecb.SetComponent<PhysicsVelocity>(backboneEnt, new() {
                    Angular = new float3(0, 0, 1f),
                });
                //if (i == 0 || i == ChainLength -1) {
                //    ecb.SetComponent<PhysicsMass>(backboneEnt, new() {
                //        AngularExpansionFactor = 0.6383572f,
                //        InverseMass = 0.0001f,
                //        InverseInertia = new float3(0.0006f, 0.001153846f, 0.001153846f),
                //        Transform = RigidTransform.identity,
                //    });
                //}
                prevBackbone = backboneEnt;
                if (dnaArray.Length > i) {
                    SpawnNucleotide(dnaArray[i].value, out _, backboneEnt, pos + new float3(1, 0, 0), ref ecb, ref connectableLookup, ref socketLookup, in typeLookup, in prefabs);
                    storage.Remove(dnaArray[i].value);
                }
            }

            //spawn nucleotides
            while (storage.Count() > 0) {
                byte type = 0;
                if (storage.adenine > 0) {
                    storage.adenine--;
                    type = ActorType.Adenine;
                } else if (storage.cytosine > 0) {
                    storage.cytosine--;
                    type = ActorType.Cytosine;
                } else if (storage.guanine > 0) {
                    storage.guanine--;
                    type = ActorType.Guanine;
                } else if (storage.thymine > 0) {
                    storage.thymine--;
                    type = ActorType.Thymine;
                }
                var pos = new float3(rand.NextFloat(type.MinXInReplicationField(), type.MaxXInReplicationField()),
                ReplicationY, rand.NextFloat(type.MinZInReplicationField(), type.MaxZInReplicationField()));
                SpawnNucleotide(in type, out _, Entity.Null, pos, ref ecb, ref connectableLookup, ref socketLookup, in typeLookup, in prefabs);
            }
        }
    }

    public static void SpawnNucleotide(
            in byte type,
            out Entity nucleotideEnt,
            in Entity connectedTo,
            in float3 pos,
            ref EntityCommandBuffer ecb,
            ref ComponentLookup<ConnectableEntity> connectableLookup,
            ref ComponentLookup<ConnectableSocket> socketLookup,
            in ComponentLookup<ActorType> typeLookup,
            in PrefabLibrary prefabs
    ) {
        nucleotideEnt = ecb.Instantiate(prefabs.PrefabByTypeForReplication(type));
        ecb.SetComponent(nucleotideEnt, LocalTransform.FromPosition(pos));

        ecb.AddComponent<ActorType>(nucleotideEnt, new() {
            value = type,
        });

        //LimitDOF
        var limitJointEnt = ecb.CreateEntity();
        var selfPair = new PhysicsConstrainedBodyPair(
            nucleotideEnt,
            Entity.Null,
            false
        );
        ecb.AddSharedComponent<PhysicsWorldIndex>(limitJointEnt, new());
        var limitJoint = PhysicsJoint.CreateLimitedDOF(
                new RigidTransform() { pos = pos, rot = quaternion.identity },
                new bool3(false, true, false),
                new bool3(true, false, true));
        ecb.AddComponent(limitJointEnt, selfPair);
        ecb.AddComponent(limitJointEnt, limitJoint);

        if (connectedTo != Entity.Null) {
            ConnectableAttachOnContactSystem.ConnectEntities(
                nucleotideEnt,
                connectedTo,
                ref ecb,
                ref connectableLookup,
                ref socketLookup,
                in typeLookup,
                out _,
                true
            );
        } else {
            ecb.AddComponent<FreeNucleotideTag>(nucleotideEnt);
        }
    }
}
