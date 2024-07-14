using Unity.Collections;
using Unity.Entities;
using Unity.Burst;

[UpdateAfter(typeof(OrganismCheckConsumablesNearbySystem))]
public partial struct OrganismConsumeTargetSystem : ISystem {

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new ConsumeTargetJob {
            typeLookup = SystemAPI.GetComponentLookup<ActorType>(true),
            consumedLookup = SystemAPI.GetComponentLookup<Consumed>(false),
        }.Schedule(state.Dependency);
    }

    [WithAll(typeof(ActionConsumeTarget))]
    [BurstCompile]
    private partial struct ConsumeTargetJob : IJobEntity {
        [ReadOnly] public ComponentLookup<ActorType> typeLookup;
        [WriteOnly] public ComponentLookup<Consumed> consumedLookup;

        public void Execute(EnabledRefRW<ActionConsumeTarget> consumeRW, ref TargetEntity target, ref NucleotideStorage storage) {
            consumeRW.ValueRW = false;
            typeLookup.TryGetComponent(target.value, out var targetType);
            if (storage.CanAdd(targetType.value)) {
                storage.Add(targetType.value);
                consumedLookup.SetComponentEnabled(target.value, true);
            }
            target.value = default;
        }
    }
}
