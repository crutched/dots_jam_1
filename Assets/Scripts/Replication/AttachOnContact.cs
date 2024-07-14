using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Physics.Systems;
using static Unity.Physics.Math;

public struct ConnectableEntity : IComponentData {
    public Entity connectedTo;
    public Entity jointEntity;
}

public struct ConnectableSocket : IComponentData {
    public Entity connectedEntity;
}

public struct Backbone : IComponentData {
    public Entity prev;
}

public struct NucleotideTag : IComponentData { }

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial struct ConnectableAttachOnContactSystem : ISystem {

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<ConnectableSocket>();
        state.RequireForUpdate<SimulationSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new AttachOnContactJob() {
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            socketLookup = SystemAPI.GetComponentLookup<ConnectableSocket>(false),
            connectableLookup = SystemAPI.GetComponentLookup<ConnectableEntity>(false),
            typeLookup = SystemAPI.GetComponentLookup<ActorType>(true),
        }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
    }

    [BurstCompile]
    private partial struct AttachOnContactJob : ICollisionEventsJob {
        [WriteOnly] public EntityCommandBuffer ecb;
        [ReadOnly] public ComponentLookup<ActorType> typeLookup;
        public ComponentLookup<ConnectableSocket> socketLookup;
        public ComponentLookup<ConnectableEntity> connectableLookup;

        public void Execute(CollisionEvent collisionEvent) {
            var entA = collisionEvent.EntityA;
            var entB = collisionEvent.EntityB;

            if (CanConnect(entA, entB, in connectableLookup, in socketLookup, in typeLookup)) {
                ConnectEntities(entB, entA, ref ecb, ref connectableLookup, ref socketLookup, in typeLookup, out _);
                return;
            }

            if (CanConnect(entB, entA, in connectableLookup, in socketLookup, in typeLookup)) {
                ConnectEntities(entA, entB, ref ecb, ref connectableLookup, ref socketLookup, in typeLookup, out _);
            }
        }

        private bool CanConnect(
                Entity entA,
                Entity entB,
                in ComponentLookup<ConnectableEntity> connectableLookup,
                in ComponentLookup<ConnectableSocket> socketLookup,
                in ComponentLookup<ActorType> typeLookup
        ) {
            if (connectableLookup.HasComponent(entB) && socketLookup.HasComponent(entA)
                    && connectableLookup[entB].connectedTo == Entity.Null
                    && socketLookup[entA].connectedEntity == Entity.Null) {
                //both nucleotides case
                if (typeLookup.HasComponent(entA)
                        && typeLookup.HasComponent(entB)) {
                    //already bound nucleotide
                    if (connectableLookup.HasComponent(entA)
                        && (connectableLookup[entA].connectedTo == Entity.Null
                            || typeLookup.HasComponent(connectableLookup[entA].connectedTo))) {
                        return false;
                    }
                    //check complementary
                    if (typeLookup[entA].value.Complement() != typeLookup[entB].value) {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

    }

    public static void ConnectEntities(
            Entity connectable,
            Entity socket,
            ref EntityCommandBuffer ecb,
            ref ComponentLookup<ConnectableEntity> connectableLookup,
            ref ComponentLookup<ConnectableSocket> socketLookup,
            in ComponentLookup<ActorType> typeLookup,
            out Entity jointEnt,
            bool deffered = false,
            bool dontWriteToSocket = false
    ) {
        var pair = new PhysicsConstrainedBodyPair(
            connectable,
            socket,
            false
        );
        jointEnt = ecb.CreateEntity();
        ecb.AddSharedComponent<PhysicsWorldIndex>(jointEnt, new());

        ecb.AddComponent(jointEnt, pair);
        var physicsJoint = PhysicsJoint.CreateLimitedHinge(
            new BodyFrame {
                Axis = new float3(0, 0, 1),
                PerpendicularAxis = new float3(0, 0, 1),
                Position = new float3(1f, 0, 0)
            },
            new BodyFrame {
                Axis = new float3(0, 0, 1),
                PerpendicularAxis = new float3(0, 0, 1),
                Position = new float3(!deffered && typeLookup.HasComponent(socket) ? -0.3f : 0.1f, 0, 0)
            },
            math.radians(new FloatRange(-30, 30))
        );
        physicsJoint.SetImpulseEventThresholdAllConstraints(new float3(math.INFINITY));
        ecb.AddComponent(jointEnt, physicsJoint);
        ecb.SetComponent<ConnectableEntity>(connectable, new() {
            connectedTo = socket,
            jointEntity = jointEnt,
        });
        if (!deffered) {
            connectableLookup.GetRefRW(connectable).ValueRW.connectedTo = socket;
            if (!dontWriteToSocket) {
                socketLookup.GetRefRW(socket).ValueRW.connectedEntity = connectable;
            }
            ecb.RemoveComponent<FreeNucleotideTag>(connectable);
        } else {
            if (!dontWriteToSocket) {
                ecb.SetComponent<ConnectableSocket>(socket, new() {
                    connectedEntity = connectable,
                });
            }
        }
    }
}
