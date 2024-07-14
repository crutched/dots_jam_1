using Unity.Entities;

public struct PrefabLibrary : IComponentData {
    public Entity organism;
    public Entity spatialDatabase;

    public Entity adenine;
    public Entity cytosine;
    public Entity guanine;
    public Entity thymine;
    public Entity nucleotideNoRender;

    //replication
    public Entity replicationBackbone;
    public Entity replicationBackboneLeft;
    public Entity replicationAdenine;
    public Entity replicationCytosine;
    public Entity replicationGuanine;
    public Entity replicationThymine;

    public readonly Entity PrefabByTypeForReplication(byte type) {
        return type switch {
            ActorType.Adenine => replicationAdenine,
            ActorType.Cytosine => replicationCytosine,
            ActorType.Guanine => replicationGuanine,
            ActorType.Thymine => replicationThymine,
            _ => Entity.Null,
        };
    }
}
