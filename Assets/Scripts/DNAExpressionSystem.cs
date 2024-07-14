using Unity.Entities;
using Unity.Burst;

public struct DNAExpressionCompletedTag : IComponentData, IEnableableComponent { }

public partial struct DNAExpressionSystem : ISystem {
    //private Random rand;

    [BurstCompile]
    public void OnCreate(ref SystemState state) {
        //rand = Random.CreateFromIndex(6542345);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        state.Dependency = new DNAExpressionJob {
            //seed = rand.NextUInt(),

        }.Schedule(state.Dependency);
    }

    [WithDisabled(typeof(DNAExpressionCompletedTag))]
    [BurstCompile]
    private partial struct DNAExpressionJob : IJobEntity {
        //public uint seed;

        public void Execute(
                EnabledRefRW<DNAExpressionCompletedTag> enRW,
                in DynamicBuffer<DNANucleotide> dna,
                ref NucleotideStorage storage,
                ref Lifetime lifetime,
                ref Size size,
                ref AutoreplicateTime autoreplicate,
                ref MoveSpeed moveSpeed
        ) {
            //var rand = Random.CreateFromIndex((uint)(seed + ent.Index));
            if (enRW.ValueRW) { return; }
            enRW.ValueRW = true;

            storage.nucleotideCapacity = (short)dna.Length;
            storage.capacity = (short)(dna.Length * 5);

            var dnaArray = dna.AsNativeArray();
            var geneLength = 0;
            var terminatorLength = 0;
            for (int i = 0; i < dnaArray.Length; i++) {
                geneLength++;
                if (dnaArray[i].value == ActorType.Thymine) {
                    terminatorLength++;
                } else {
                    terminatorLength = 0;
                }
                if (terminatorLength == 2) {
                    var usefulLength = geneLength - terminatorLength;
                    if (usefulLength == 2) {
                        lifetime.modifierPlusPercent += 20;
                        lifetime.timeLeft = lifetime.CalculateTime();
                    }
                    if (usefulLength == 3) {
                        size.modifierMultiplier += 0.3f;
                    }
                    if (usefulLength == 4) {
                        autoreplicate.modifierMultiplier *= 0.7f;
                        autoreplicate.timeLeft = AutoreplicateTime.DefaultReplicationTime * autoreplicate.modifierMultiplier;
                    }
                    if (usefulLength == 5) {
                        moveSpeed.modifierMultiplier += 0.3f;
                    }
                    geneLength = 0;
                    terminatorLength = 0;
                }
            }
        }
    }
}
