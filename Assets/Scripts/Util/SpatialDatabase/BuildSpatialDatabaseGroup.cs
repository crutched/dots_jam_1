using Unity.Entities;

[UpdateAfter(typeof(MainThreadWorkGroup))]
public partial class BuildSpatialDatabaseGroup : ComponentSystemGroup { }
