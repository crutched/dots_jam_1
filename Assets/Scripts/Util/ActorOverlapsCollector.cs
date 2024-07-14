using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Entities;

public struct ActorOverlapsCollector : ISpatialQueryCollector {
    public Entity querierEnt;
    public float3 querierPosition;
    public byte querierType;
    public float querierRadiusSq;

    public byte matchType;
    public SpatialDatabaseElement matchedElement;

    public ActorOverlapsCollector(Entity querierEnt, float3 querierPosition, byte querierType, byte matchType, float sizeMultiplier) {
        this.querierEnt = querierEnt;
        this.querierPosition = querierPosition;
        this.matchType = matchType;
        this.querierType = querierType;
        this.querierRadiusSq = querierType.Radiussq() * sizeMultiplier;
        matchedElement = default;
    } 

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnVisitCell(in SpatialDatabaseCell cell, in UnsafeList<SpatialDatabaseElement> elements, out bool shouldEarlyExit) {
        shouldEarlyExit = false;
        for (int i = cell.StartIndex; i < cell.StartIndex + cell.ElementsCount; i++) {
            SpatialDatabaseElement element = elements[i];
            if ((element.Type & matchType) > 0) {
                float distSq = math.distancesq(querierPosition, element.Position);
                if (distSq < (querierRadiusSq + element.Type.Radiussq())) {
                    if (element.Entity.Index != querierEnt.Index) {
                        matchedElement = element;
                        shouldEarlyExit = true;
                        break;
                    }
                }
            }
        }
    }
}
