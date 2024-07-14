using Unity.Collections;
using Unity.Transforms;
using Unity.Entities;
using Unity.Burst;
using Unity.Burst.Intrinsics;

public partial struct OrganismCheckConsumablesNearbySystem : ISystem {

#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<SpatialDatabaseSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new SearchMoleculesNearbyJob {
            consumeTargetLookup = SystemAPI.GetComponentLookup<ActionConsumeTarget>(false),
            targetLookup = SystemAPI.GetComponentLookup<TargetEntity>(false),
            cachedDB = new CachedSpatialDatabaseRO {
                SpatialDatabaseEntity = SystemAPI.GetSingleton<SpatialDatabaseSingleton>().TargetablesSpatialDatabase,
                SpatialDatabaseLookup = SystemAPI.GetComponentLookup<SpatialDatabase>(true),
                CellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(true),
                ElementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(true),
            },
        }.ScheduleParallel(state.Dependency);
    }

    [WithAll(typeof(TargetEntity))]
    [WithPresent(typeof(ActionConsumeTarget))]
    [WithAll(typeof(OrganismTag))]
    [BurstCompile]
    private partial struct SearchMoleculesNearbyJob : IJobEntity, IJobEntityChunkBeginEnd {
        public CachedSpatialDatabaseRO cachedDB;
        [NativeDisableParallelForRestriction] [WriteOnly] public ComponentLookup<ActionConsumeTarget> consumeTargetLookup;
        [NativeDisableParallelForRestriction] [WriteOnly] public ComponentLookup<TargetEntity> targetLookup;

        public void Execute(Entity ent, in NucleotideStorage storage, in ActorType actorType, in LocalToWorld ltw, in Size size) {
            var neededMask = storage.MaskForNeeded();
            if (neededMask == 0) { return; }
            ActorOverlapsCollector collector =
                new ActorOverlapsCollector(ent, ltw.Position, actorType.value, neededMask, size.modifierMultiplier);
            SpatialDatabase.QueryAABBCellProximityOrder(in cachedDB._SpatialDatabase,
                in cachedDB._SpatialDatabaseCells,
                in cachedDB._SpatialDatabaseElements, ltw.Position,
                0.2f, ref collector);

            if (collector.matchedElement.Entity != Entity.Null) {
                //UnityEngine.Debug.Log("found");
                targetLookup.GetRefRW(ent).ValueRW.value = collector.matchedElement.Entity;
                consumeTargetLookup.SetComponentEnabled(ent, true);
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
            cachedDB.CacheData();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask, bool chunkWasExecuted) {
        }
    }
}
