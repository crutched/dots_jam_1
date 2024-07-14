using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class PrefabLibraryAuthoring : MonoBehaviour {
    public GameObject organism;
    public GameObject spatialDB;

    public GameObject adenine;
    public GameObject cytosine;
    public GameObject guanine;
    public GameObject thymine;
    public GameObject noRender;

    public GameObject replicationBackbone;
    public GameObject replicationBackboneLeft;
    public GameObject replicationAdenine;
    public GameObject replicationCytosine;
    public GameObject replicationGuanine;
    public GameObject replicationThymine;

    public class Baker : Baker<PrefabLibraryAuthoring> {

        public override void Bake(PrefabLibraryAuthoring authoring) {
            var ent = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PrefabLibrary>(ent, new() {
                organism = GetEntity(authoring.organism, TransformUsageFlags.WorldSpace),
                spatialDatabase = GetEntity(authoring.spatialDB, TransformUsageFlags.None),

                adenine = GetEntity(authoring.adenine, TransformUsageFlags.None),
                cytosine = GetEntity(authoring.cytosine, TransformUsageFlags.None),
                guanine = GetEntity(authoring.guanine, TransformUsageFlags.None),
                thymine = GetEntity(authoring.thymine, TransformUsageFlags.None),
                nucleotideNoRender = GetEntity(authoring.noRender, TransformUsageFlags.None),

                replicationAdenine = GetEntity(authoring.replicationAdenine, TransformUsageFlags.Dynamic),
                replicationCytosine = GetEntity(authoring.replicationCytosine, TransformUsageFlags.Dynamic),
                replicationBackbone = GetEntity(authoring.replicationBackbone, TransformUsageFlags.Dynamic),
                replicationBackboneLeft = GetEntity(authoring.replicationBackboneLeft, TransformUsageFlags.Dynamic),
                replicationGuanine = GetEntity(authoring.replicationGuanine, TransformUsageFlags.Dynamic),
                replicationThymine = GetEntity(authoring.replicationThymine, TransformUsageFlags.Dynamic),
            });
        }
    }
}
