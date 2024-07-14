using Unity.Entities;
using Unity.Mathematics;

public struct OrganismTag : IComponentData { }

public struct AutoreplicateTime : IComponentData {
    public float timeLeft;
    public float modifierMultiplier;

    public const float DefaultReplicationTime = 8f;
}

public struct MoveSpeed : IComponentData {
    public float modifierMultiplier;
}

public struct Size : IComponentData {
    public float modifierMultiplier;
}

public struct Lifetime : IComponentData {
    public float timeLeft;
    public short modifierPlusPercent;

    public const float DefaultLifetime = 17f;

    public readonly float CalculateTime() {
        return DefaultLifetime + (DefaultLifetime * (modifierPlusPercent / 100f));
    }
}

[InternalBufferCapacity(0)]
public struct DNANucleotide : IBufferElementData {
    public byte value;
}

public struct NucleotideStorage : IComponentData {
    public short adenine;
    public short cytosine;
    public short guanine;
    public short thymine;

    public short nucleotideCapacity;
    public short capacity;

    public readonly bool IsFull() {
        return (adenine + cytosine + guanine + thymine > capacity) ||
            (adenine == nucleotideCapacity && cytosine == nucleotideCapacity && guanine == nucleotideCapacity && thymine == nucleotideCapacity);
    }

    public readonly bool CanAdd(byte type) {
        if (IsFull()) {
            return false;
        }
        return type switch {
            ActorType.Adenine => adenine < nucleotideCapacity,
            ActorType.Cytosine => cytosine < nucleotideCapacity,
            ActorType.Guanine => guanine < nucleotideCapacity,
            ActorType.Thymine => thymine < nucleotideCapacity,
            _ => false,
        };
    }

    public readonly byte MaskForNeeded() {
        byte mask = 0;
        if (CanAdd(ActorType.Adenine)) {
            mask |= ActorType.Adenine;
        }
        if (CanAdd(ActorType.Cytosine)) {
            mask |= ActorType.Cytosine;
        }
        if (CanAdd(ActorType.Guanine)) {
            mask |= ActorType.Guanine;
        }
        if (CanAdd(ActorType.Thymine)) {
            mask |= ActorType.Thymine;
        }
        return mask;
    }

    public readonly int Count() {
        return adenine + cytosine + guanine + thymine;
    }

    public bool Add(byte type) {
        short result = type switch {
            ActorType.Adenine => ++adenine,
            ActorType.Cytosine => ++cytosine,
            ActorType.Guanine => ++guanine,
            ActorType.Thymine => ++thymine,
            _ => default
        };
        return result != 0;
    }

    public bool Remove(byte type) {
        short result = type switch {
            ActorType.Adenine => --adenine,
            ActorType.Cytosine => --cytosine,
            ActorType.Guanine => --guanine,
            ActorType.Thymine => --thymine,
            _ => default,
        };
        return result >= 0;
    }
}

public struct Generation : IComponentData {
    public ushort value;
}

public static partial class NucleotideExtensions {

    public static byte Complement(this byte nucleotide) {
        return nucleotide switch {
            1 => 8,
            8 => 1,
            2 => 4,
            4 => 2,
            _ => 0,
        };
    }
}
