using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class OrganismAuthoring : MonoBehaviour {
    public byte type;

    public class Baker : Baker<OrganismAuthoring> {

        public override void Bake(OrganismAuthoring authoring) {
            var ent = GetEntity(TransformUsageFlags.WorldSpace);
            if (authoring.type == ActorType.Organism) {
                AddComponent<OrganismTag>(ent);
                AddComponent<NucleotideStorage>(ent);
                AddComponent<TargetEntity>(ent);
                AddComponent<ActionConsumeTarget>(ent);
                SetComponentEnabled<ActionConsumeTarget>(ent, false);
                AddComponent<DNAExpressionCompletedTag>(ent);
                SetComponentEnabled<DNAExpressionCompletedTag>(ent, false);
                AddBuffer<DNANucleotide>(ent);
                AddComponent<Generation>(ent);
                AddComponent<AutoreplicateTime>(ent, new() {
                    timeLeft = AutoreplicateTime.DefaultReplicationTime,
                    modifierMultiplier = 1.0f,
                });
                AddComponent<Lifetime>(ent, new() {
                    timeLeft = Lifetime.DefaultLifetime,
                });
                AddComponent<Size>(ent, new() {
                    modifierMultiplier = 1f,
                });
            }
            AddComponent<ActorType>(ent, new() {
                value = authoring.type,
            });
            AddComponent<SpatialDatabaseCellIndex>(ent);
            AddComponent<Consumed>(ent);
            AddComponent<BelongsToDishTag>(ent);
            SetComponentEnabled<Consumed>(ent, false);
            AddComponent<MoveSpeed>(ent, new() {
                modifierMultiplier = 1.0f,
            });
        }
    }
}
