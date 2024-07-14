using Unity.Transforms;
using Unity.Entities;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MainCameraSystem : SystemBase {

    protected override void OnCreate() {
        base.OnCreate();
        RequireForUpdate<MainCamera>();
    }

    protected override void OnUpdate() {
        LocalToWorld mainCameraLtW = SystemAPI.GetComponent<LocalToWorld>(SystemAPI.GetSingletonEntity<MainCamera>());
        if (CamerasRef.instance.mainCamera != null) {
            CamerasRef.instance.mainCamera.transform.SetPositionAndRotation(mainCameraLtW.Position, mainCameraLtW.Rotation);
        }
    }
}
