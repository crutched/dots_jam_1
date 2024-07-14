using Unity.Collections;
using Unity.Entities;
using Unity.Burst;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial struct DestroyConsumedSystem : ISystem {

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new DestroyConsumedJob {
            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
        }.Schedule(state.Dependency);
    }

    [WithAll(typeof(Consumed))]
    [BurstCompile]
    private partial struct DestroyConsumedJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;

        public void Execute(Entity ent) {
            ecb.DestroyEntity(ent);
        }
    }
}
