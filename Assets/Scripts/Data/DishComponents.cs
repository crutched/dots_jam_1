using Unity.Entities;

public struct Dish : IComponentData {
    public int organisms;
    public int nucleotides;

    public float diameter;
    public float radius;
    public float height;

    public float radiussq;
}
