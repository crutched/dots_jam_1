using Unity.Transforms;
using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
partial struct BakingSystem : ISystem {

    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<Prefab>();
    }

    public void OnUpdate(ref SystemState state) {
        var prefabQuery = SystemAPI.QueryBuilder().WithAll<Prefab, ActorType>().WithOptions(EntityQueryOptions.IncludePrefab).Build();
        state.EntityManager.RemoveComponent<LinkedEntityGroup>(prefabQuery);
        state.EntityManager.RemoveComponent<LocalTransform>(prefabQuery);
    }
}
