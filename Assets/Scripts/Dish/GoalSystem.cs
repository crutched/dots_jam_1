using Unity.Collections;
using Unity.Entities;
using Unity.Burst;

public struct FinishGameTag : IComponentData {
    public const int DNASizeGoal = 32;
}

public partial struct GoalSystem : ISystem {

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<Dish>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        if (SystemAPI.QueryBuilder().WithAll<FinishGameTag>().Build().IsEmptyIgnoreFilter) {
            state.Dependency = new GoalCheckJob {
                ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(state.Dependency);
        }
        if (!SystemAPI.QueryBuilder().WithAll<FinishGameTag>().Build().IsEmptyIgnoreFilter
                && !SystemAPI.QueryBuilder().WithAll<BelongsToDishTag>().Build().IsEmptyIgnoreFilter) {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            ecb.DestroyEntity(SystemAPI.QueryBuilder().WithAll<BelongsToDishTag>().WithNone<Consumed>().Build(), EntityQueryCaptureMode.AtRecord);
        }
    }

    [BurstCompile]
    private partial struct GoalCheckJob : IJobEntity {
        [WriteOnly] public EntityCommandBuffer ecb;

        public void Execute(in DynamicBuffer<DNANucleotide> dna) {
            if (dna.Length >= FinishGameTag.DNASizeGoal) {
                var finish = ecb.CreateEntity();
                ecb.AddComponent<FinishGameTag>(finish);
            }
        }
    }
}
