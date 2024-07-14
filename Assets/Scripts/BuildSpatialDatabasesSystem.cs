//Spatial database sources: https://github.com/Unity-Technologies/ECSGalaxySample/tree/main
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(BuildSpatialDatabaseGroup))]
public partial struct BuildSpatialDatabasesSystem : ISystem {
    private EntityQuery _spatialDatabasesQuery;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _spatialDatabasesQuery = SystemAPI.QueryBuilder().WithAll<SpatialDatabase, SpatialDatabaseCell, SpatialDatabaseElement>().Build();
        
        state.RequireForUpdate<SpatialDatabaseSingleton>();
        state.RequireForUpdate(_spatialDatabasesQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        SpatialDatabaseSingleton spatialDatabaseSingleton = SystemAPI.GetSingleton<SpatialDatabaseSingleton>();

        CachedSpatialDatabaseUnsafe cachedSpatialDatabase = new CachedSpatialDatabaseUnsafe
        {
            SpatialDatabaseEntity = spatialDatabaseSingleton.TargetablesSpatialDatabase,
            SpatialDatabaseLookup = SystemAPI.GetComponentLookup<SpatialDatabase>(false),
            CellsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseCell>(false),
            ElementsBufferLookup = SystemAPI.GetBufferLookup<SpatialDatabaseElement>(false),
        };

        // Make each ship calculate the octant it belongs to
        SpatialDatabaseParallelComputeCellIndexJob cellIndexJob = new SpatialDatabaseParallelComputeCellIndexJob
        {
            CachedSpatialDatabase = cachedSpatialDatabase,
        };
        state.Dependency = cellIndexJob.ScheduleParallel(state.Dependency);
            
        // Launch X jobs, each responsible for 1/Xth of spatial database cells
        JobHandle initialDep = state.Dependency;
        int parallelCount = math.max(1, JobsUtility.JobWorkerCount - 2);
        for (int s = 0; s < parallelCount; s++)
        {
            BuildSpatialDatabaseParallelJob buildJob = new BuildSpatialDatabaseParallelJob
            {
                JobSequenceNb = s,
                JobsTotalCount = parallelCount,
                CachedSpatialDatabase = cachedSpatialDatabase,
            };
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, buildJob.Schedule(initialDep));
        }
    }

    [BurstCompile]
    public partial struct SpatialDatabaseParallelComputeCellIndexJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public CachedSpatialDatabaseUnsafe CachedSpatialDatabase;
        
        // other cached data
        private UniformOriginGrid _grid;
        
        public void Execute(in LocalToWorld ltw, ref SpatialDatabaseCellIndex sdCellIndex)
        {
            sdCellIndex.CellIndex = UniformOriginGrid.GetCellIndex(in _grid, ltw.Position);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            CachedSpatialDatabase.CacheData();
            _grid = CachedSpatialDatabase._SpatialDatabase.Grid;
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }

    [BurstCompile]
    public partial struct BuildSpatialDatabaseParallelJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public int JobSequenceNb;
        public int JobsTotalCount;
        public CachedSpatialDatabaseUnsafe CachedSpatialDatabase;
        
        public void Execute(Entity entity, in LocalToWorld ltw, in SpatialDatabaseCellIndex sdCellIndex, in ActorType actorType)
        {
            if (sdCellIndex.CellIndex % JobsTotalCount == JobSequenceNb)
            {
                var element = new SpatialDatabaseElement {
                    Entity = entity,
                    Position = ltw.Position,
                    Type = actorType.value,
                };
                SpatialDatabase.AddToDataBase(in CachedSpatialDatabase._SpatialDatabase,
                    ref CachedSpatialDatabase._SpatialDatabaseCells, ref CachedSpatialDatabase._SpatialDatabaseElements,
                    element, sdCellIndex.CellIndex);
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            CachedSpatialDatabase.CacheData();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }
}
