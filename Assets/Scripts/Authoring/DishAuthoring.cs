using Unity.Mathematics;
using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class DishAuthoring : MonoBehaviour {
    public float diameter;
    public float heightRatio;

    public int organisms;
    public int proteins;

    public class Baker : Baker<DishAuthoring> {

        public override void Bake(DishAuthoring authoring) {
            var ent = GetEntity(TransformUsageFlags.Dynamic);
            var radius = authoring.diameter / 2f;
            AddComponent<Dish>(ent, new() {
                diameter = authoring.diameter,
                radius = radius,
                height = authoring.diameter / authoring.heightRatio,
                radiussq = math.pow(radius, 2),
                organisms = authoring.organisms,
                nucleotides = authoring.proteins,
            });
        }
    }
}
