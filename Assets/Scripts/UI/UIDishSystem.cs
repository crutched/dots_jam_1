using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Unity.Entities;
using UnityEngine;
using System;

[UpdateInGroup(typeof(MainThreadWorkGroup))]
public partial class UIDishSystem : SystemBase {
    private UIDocument dishDoc;
    private Label organismInfo;
    private LiftGammaGain post;

    private UIDocument globalDoc;
    private Label globalInfo;

    private UIDocument replicationDoc;

    private UIDocument finalDoc;

    protected override void OnStartRunning() {
        base.OnStartRunning();
        dishDoc = UIRef.instance.dishUI;
        organismInfo = dishDoc.rootVisualElement.Q<Label>("organismInfo");
        UIRef.instance.postprocessing.profile.TryGet(out post);

        globalDoc = UIRef.instance.globalUI;
        globalInfo = globalDoc.rootVisualElement.Q<Label>("globalInfo");

        finalDoc = UIRef.instance.finalUI;
        finalDoc.enabled = false;

        replicationDoc = UIRef.instance.replicationUI;
    }

    protected override void OnCreate() {
        base.OnCreate();
        RequireForUpdate<GameCamera>();
    }

    private const string TimeFormat = @"mm\:ss\:f";

    protected override void OnUpdate() {
        // final check
        if (!SystemAPI.QueryBuilder().WithAll<FinishGameTag>().Build().IsEmptyIgnoreFilter) {
            finalDoc.enabled = true;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
                EntityManager.DestroyEntity(SystemAPI.QueryBuilder().WithAll<FinishGameTag>().Build());
                var initDish = EntityManager.CreateEntity();
                EntityManager.AddComponent<SeedDishSystem.DishNotInitialized>(initDish);
            }
            return;
        } else {
            finalDoc.enabled = false;
        }

        // global UI
        TimeSpan simulationTime = TimeSpan.FromSeconds(SystemAPI.Time.ElapsedTime);

        globalInfo.text = $@"{simulationTime.ToString(TimeFormat)}
Number of organisms: {SystemAPI.QueryBuilder().WithAll<OrganismTag>().Build().CalculateEntityCountWithoutFiltering()}
Number of nucleotides: {SystemAPI.QueryBuilder().WithAll<ActorType, BelongsToDishTag>().WithNone<OrganismTag>().Build().CalculateEntityCountWithoutFiltering()}

Goal: DNA with {FinishGameTag.DNASizeGoal} pairs
";

        if (!SystemAPI.QueryBuilder().WithAll<BelongsToReplicationTag>().Build().IsEmptyIgnoreFilter) {
            // replication UI
            dishDoc.rootVisualElement.style.display = DisplayStyle.None;
            post.gamma.value = new Vector4(1, 1, 1, -0.31f);
            post.lift.value = new Vector4(1, 1, 1, -0.62f);
            replicationDoc.rootVisualElement.style.display = DisplayStyle.Flex;
            return;
        }

        // dish UI
        dishDoc.rootVisualElement.style.display = DisplayStyle.Flex;
        replicationDoc.rootVisualElement.style.display = DisplayStyle.None;
        post.gamma.value = new Vector4(1, 1, 1, 0);
        post.lift.value = new Vector4(1, 1, 1, 0);
        var cameraRO = SystemAPI.GetSingleton<GameCamera>();
        if (cameraRO.FollowedEntity != Entity.Null && SystemAPI.GetEntityStorageInfoLookup().Exists(cameraRO.FollowedEntity)) {
            var generation = SystemAPI.GetComponent<Generation>(cameraRO.FollowedEntity);
            var storage = SystemAPI.GetComponent<NucleotideStorage>(cameraRO.FollowedEntity);
            var readyForReplication = storage.Count() > storage.capacity / 2 ? "Press R to start replication" : "";
            var dna = SystemAPI.GetBuffer<DNANucleotide>(cameraRO.FollowedEntity);

            TimeSpan autoReplicateTime = TimeSpan.FromSeconds(SystemAPI.GetComponent<AutoreplicateTime>(cameraRO.FollowedEntity).timeLeft);
            TimeSpan lifeTime = TimeSpan.FromSeconds(SystemAPI.GetComponent<Lifetime>(cameraRO.FollowedEntity).timeLeft);

            var size = SystemAPI.GetComponent<Size>(cameraRO.FollowedEntity);
            var moveSpeed = SystemAPI.GetComponent<MoveSpeed>(cameraRO.FollowedEntity);
            var sizeText = size.modifierMultiplier != 1.0f ? string.Format("\n +{0} consuming radius", (size.modifierMultiplier * 100) - 100) : "";
            var moveText = moveSpeed.modifierMultiplier != 1.0f ? string.Format("\b +{0} movement speed", (moveSpeed.modifierMultiplier * 100) - 100) : "";

            organismInfo.text = @$"Selected organism info:
 - Generation: {generation.value}
 - DNA length: {dna.Length} pairs
 - Nucleotide storage:
 -- adenine: {storage.adenine} of {storage.nucleotideCapacity}
 -- cytosine: {storage.cytosine} of {storage.nucleotideCapacity}
 -- guanine: {storage.guanine} of {storage.nucleotideCapacity}
 -- thymine: {storage.thymine} of {storage.nucleotideCapacity}
 --- total: {storage.Count()} of {storage.capacity}
Auto-replication in: {autoReplicateTime.ToString(TimeFormat)}
Death in: {lifeTime.ToString(TimeFormat)}{sizeText}{moveText}


{readyForReplication}";
        } else {
            organismInfo.text = "Press Z to follow some organism";
        }
    }
}
