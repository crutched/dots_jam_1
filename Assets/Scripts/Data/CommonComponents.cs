using Unity.Mathematics;
using Unity.Entities;

public struct ActorType : IComponentData {
    public byte value;

    public const byte Organism = 128;

    public const byte Adenine = 1;
    public const byte Cytosine = 2;
    public const byte Guanine = 4;
    public const byte Thymine = 8;

    public const byte Nucleotide = Adenine | Cytosine | Guanine | Thymine;
}

public static partial class ActorTypeExtensions {

    public static float Radiussq(this byte actorType) {
        return actorType switch {
            ActorType.Organism => 0.1f,
            ActorType.Adenine => 0.01f,
            ActorType.Guanine => 0.01f,
            ActorType.Thymine => 0.01f,
            ActorType.Cytosine => 0.01f,
            _ => 0,
        };
    }

    //bounds (-18.5 2) -8,25 (6 -9.5) -1.75
    public static float MinXInReplicationField(this byte type) {
        return type switch {
            ActorType.Adenine or ActorType.Thymine => -18f,
            ActorType.Cytosine or ActorType.Guanine => -7.5f,
            _ => 0f,
        };
    }

    public static float MaxXInReplicationField(this byte type) {
        return type switch {
            ActorType.Cytosine or ActorType.Guanine => 2f,
            ActorType.Adenine or ActorType.Thymine => -8.75f,
            _ => 0f,
        };
    }

    public static float MaxZInReplicationField(this byte type) {
        return type switch {
            ActorType.Adenine or ActorType.Cytosine => 5.5f,
            ActorType.Guanine or ActorType.Thymine => -2.5f,
            _ => 0f,
        };
    }

    public static float MinZInReplicationField(this byte type) {
        return type switch {
            ActorType.Adenine or ActorType.Cytosine => -1f,
            ActorType.Guanine or ActorType.Thymine => -8.75f,
            _ => 0f,
        };
    }
}

public struct ActionConsumeTarget : IEnableableComponent, IComponentData { }

public struct TargetEntity : IComponentData {
    public Entity value;
}

public struct MoveDirection : IComponentData {
    public float3 value;
}

//TODO: move to spatial db
public struct SpatialDatabaseSingleton : IComponentData {
    public Entity TargetablesSpatialDatabase;
}

public struct Consumed : IEnableableComponent, IComponentData { }
