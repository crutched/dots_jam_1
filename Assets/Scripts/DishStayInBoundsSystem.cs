using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;

[UpdateAfter(typeof(MoveSystem))]
public partial struct DishStayInBoundsSystem : ISystem {

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<Dish>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new StayInBoundsJob {
            dish = SystemAPI.GetSingleton<Dish>(),
            moveLookup = SystemAPI.GetComponentLookup<MoveDirection>(false),
        }.ScheduleParallel(state.Dependency);
    }

    [WithAll(typeof(MoveDirection))]
    [BurstCompile]
    private partial struct StayInBoundsJob : IJobEntity {
        [ReadOnly] public Dish dish;
        [NativeDisableParallelForRestriction] public ComponentLookup<MoveDirection> moveLookup;

        public void Execute(Entity ent, in LocalToWorld ltw) {
            var position = ltw.Position;

            var normal = float3.zero;
            if (position.y < 0) {
                normal = math.up();
            } else if (position.y > dish.height) {
                normal = math.down();
            } else if (math.lengthsq(new float2(position.x, position.z)) > dish.radiussq) {
                normal = math.normalizesafe(float3.zero - position);
            }
            if (!normal.Equals(float3.zero)) {
                var direction = moveLookup.GetRefRW(ent);
                if (math.dot(direction.ValueRW.value, normal) < 0) {
                    direction.ValueRW.value = direction.ValueRW.value - 2 * math.dot(direction.ValueRW.value, normal) * normal;
                }
            }
        }
    }
}
