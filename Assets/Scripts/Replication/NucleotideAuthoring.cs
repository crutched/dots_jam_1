using Unity.Entities;
using UnityEngine;

public class NucleotideAuthoring : MonoBehaviour {

    class Baker : Baker<NucleotideAuthoring> {

        public override void Bake(NucleotideAuthoring authoring) {
            var ent = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<NucleotideTag>(ent);
        }
    }
}
