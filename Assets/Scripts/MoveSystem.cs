using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;

public partial struct MoveSystem : ISystem {

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new MoveJob {
            deltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    private partial struct MoveJob : IJobEntity {
        [ReadOnly] public float deltaTime;

        public void Execute(ref LocalToWorld ltw, in MoveDirection moveDirection, in MoveSpeed moveSpeed) {
            ltw.Value = LocalTransform.FromPositionRotation(ltw.Position + moveDirection.value * deltaTime * moveSpeed.modifierMultiplier, ltw.Rotation).ToMatrix();
        }
    }
}

public partial struct MoveInitSystem : ISystem {
    private Random rand;

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BelongsToDishTag, ActorType>().WithNone<MoveDirection>().Build());
        rand = Random.CreateFromIndex(1234245);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new MoveInitJob {
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            rand = Random.CreateFromIndex(rand.NextUInt()),
        }.Schedule(state.Dependency);
    }

    [WithAll(typeof(BelongsToDishTag))]
    [WithAll(typeof(ActorType))]
    [WithNone(typeof(MoveDirection))]
    [BurstCompile]
    private partial struct MoveInitJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;

        public Random rand;

        public void Execute(Entity ent) {
            ecb.AddComponent<MoveDirection>(ent, new() {
                value = rand.NextFloat3Direction(),
            });
        }
    }
}