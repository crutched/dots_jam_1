using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
public partial struct GameCameraSystem : ISystem {
    private Unity.Mathematics.Random _random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _random = Unity.Mathematics.Random.CreateFromIndex(12121212);
            
        state.RequireForUpdate<SimulationRate>();
        state.RequireForUpdate<GameCamera>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Collect input
        CameraInputs cameraInputs = new CameraInputs
        {
            Move = new float3(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) + (Input.GetKey(KeyCode.Q) ? -1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f)),
            Look = new float2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y")),
            Zoom = -Input.mouseScrollDelta.y,
            Sprint = Input.GetKey(KeyCode.LeftShift),
            SwitchMode = Input.GetKeyDown(KeyCode.Z),
        };
        cameraInputs.Move = math.normalizesafe(cameraInputs.Move) *
                            math.saturate(math.length(cameraInputs.Move)); // Clamp move inputs magnitude to 1

        Entity nextTargetOrganism = Entity.Null;
        if (SystemAPI.QueryBuilder().WithAll<BelongsToReplicationTag>().Build().IsEmptyIgnoreFilter) {
            //available in non-replication mode only
            //start replication
            var cameraRO = SystemAPI.GetSingleton<GameCamera>();
            if (Input.GetKeyDown(KeyCode.R)) {
                if (cameraRO.FollowedEntity != Entity.Null) {
                    var storage = SystemAPI.GetComponent<NucleotideStorage>(cameraRO.FollowedEntity);
                    if (storage.Count() > storage.capacity / 2) {
                        state.EntityManager.AddComponent<ReplicationStartSystem.ReplicationInit>(cameraRO.FollowedEntity);
                    }
                }
            }

            foreach (var (_, ent) in SystemAPI.Query<SelectNewOrganismTag>().WithAll<OrganismTag>().WithEntityAccess()) {
                nextTargetOrganism = ent;
                break;
            }
            state.EntityManager.RemoveComponent<SelectNewOrganismTag>(SystemAPI.QueryBuilder().WithAll<SelectNewOrganismTag, OrganismTag>().Build());

            // Camera target switching
            bool switchOrganism = Input.GetKeyDown(KeyCode.X);
            if (cameraInputs.SwitchMode || switchOrganism || (nextTargetOrganism == Entity.Null && !SystemAPI.Exists(cameraRO.FollowedEntity))) {
                EntityQuery organismQuery = SystemAPI.QueryBuilder().WithAll<OrganismTag>().Build();
                NativeArray<Entity> organismEntities = organismQuery.ToEntityArray(Allocator.Temp);
                if (organismEntities.Length > 0) {
                    nextTargetOrganism = organismEntities[_random.NextInt(organismEntities.Length)];
                }
                organismEntities.Dispose();
            }
        } else {
            //replication only
            if (Input.GetKeyDown(KeyCode.T)) {
                var cameraRO = SystemAPI.GetSingleton<GameCamera>();
                if (cameraRO.FollowedEntity != Entity.Null) {
                    state.EntityManager.AddComponent<ReplicationTerminationTag>(cameraRO.FollowedEntity);
                }
            }
        }

        GameCameraJob job = new GameCameraJob
        {
            DeltaTime = SystemAPI.GetSingleton<SimulationRate>().UnscaledDeltaTime,
            CameraInputs = cameraInputs,
            nextTargetOrganism = nextTargetOrganism,
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false),
            moveDirectionLookup = SystemAPI.GetComponentLookup<MoveDirection>(true),
        };
        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct GameCameraJob : IJobEntity
    {
        public float DeltaTime;
        public CameraInputs CameraInputs;
        public Entity nextTargetOrganism;

        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly] public ComponentLookup<MoveDirection> moveDirectionLookup;

        void Execute(Entity entity, ref LocalTransform transform, ref GameCamera gameCamera)
        {
            if (gameCamera.IgnoreInput)
                return;

            // Mode switch
            if (CameraInputs.SwitchMode)
            {
                switch (gameCamera.CameraMode)
                {
                    case GameCamera.Mode.Fly:
                        gameCamera.CameraMode = GameCamera.Mode.FollowOrganism;
                        break;
                    case GameCamera.Mode.FollowOrganism:
                        gameCamera.CameraMode = GameCamera.Mode.Fly;
                        break;
                }
            }

            // Target switch
            if (nextTargetOrganism != Entity.Null)
            {
                switch (gameCamera.CameraMode) {
                    case GameCamera.Mode.FollowOrganism:
                        gameCamera.FollowedEntity = nextTargetOrganism;
                        break;
                }
            }

            switch (gameCamera.CameraMode)
            {
                case GameCamera.Mode.Fly:
                {
                    // Yaw
                    float yawAngleChange = CameraInputs.Look.x * gameCamera.FlyRotationSpeed;
                    quaternion yawRotation = quaternion.Euler(math.up() * math.radians(yawAngleChange));
                    gameCamera.PlanarForward = math.mul(yawRotation, gameCamera.PlanarForward);

                    // Pitch
                    gameCamera.PitchAngle += -CameraInputs.Look.y * gameCamera.FlyRotationSpeed;
                    gameCamera.PitchAngle = math.clamp(gameCamera.PitchAngle, gameCamera.MinVAngle,
                        gameCamera.MaxVAngle);
                    quaternion pitchRotation = quaternion.Euler(math.right() * math.radians(gameCamera.PitchAngle));

                    // Final rotation
                    quaternion targetRotation =
                        math.mul(quaternion.LookRotationSafe(gameCamera.PlanarForward, math.up()), pitchRotation);
                    transform.Rotation = math.slerp(transform.Rotation, targetRotation,
                        MathUtilities.GetSharpnessInterpolant(gameCamera.FlyRotationSharpness, DeltaTime));

                    // Move
                    float3 worldMoveInputs = math.rotate(transform.Rotation, CameraInputs.Move);
                    float finalMaxSpeed = gameCamera.FlyMaxMoveSpeed;
                    if (CameraInputs.Sprint)
                    {
                        finalMaxSpeed *= gameCamera.FlySprintSpeedBoost;
                    }

                    gameCamera.CurrentMoveVelocity = math.lerp(gameCamera.CurrentMoveVelocity,
                        worldMoveInputs * finalMaxSpeed,
                        MathUtilities.GetSharpnessInterpolant(gameCamera.FlyMoveSharpness, DeltaTime));
                    transform.Position += gameCamera.CurrentMoveVelocity * DeltaTime;

                    break;
                }
                case GameCamera.Mode.FollowOrganism:
                {
                    // if there is a followed entity, place the camera relatively to it
                    if (LocalToWorldLookup.TryGetComponent(gameCamera.FollowedEntity, out LocalToWorld followedLTW))
                    {
                        //// Rotation
                        //{
                        //    transform.Rotation = quaternion.LookRotationSafe(gameCamera.PlanarForward, math.up());

                        //    // Yaw
                        //    float yawAngleChange = CameraInputs.Look.x * gameCamera.OrbitRotationSpeed;
                        //    quaternion yawRotation = quaternion.Euler(math.up() * math.radians(yawAngleChange));
                        //    gameCamera.PlanarForward = math.rotate(yawRotation, gameCamera.PlanarForward);
                                

                        //    // Pitch
                        //    gameCamera.PitchAngle += -CameraInputs.Look.y * gameCamera.OrbitRotationSpeed;
                        //    gameCamera.PitchAngle = math.clamp(gameCamera.PitchAngle, gameCamera.MinVAngle,
                        //        gameCamera.MaxVAngle);
                        //    quaternion pitchRotation =
                        //        quaternion.Euler(math.right() * math.radians(gameCamera.PitchAngle));

                        //    // Final rotation
                        //    transform.Rotation = quaternion.LookRotationSafe(gameCamera.PlanarForward, math.up());
                        //    transform.Rotation = math.mul(transform.Rotation, pitchRotation);
                        //}

                        if (moveDirectionLookup.TryGetComponent(gameCamera.FollowedEntity, out var moveDirection)) {
                            var desiredRot = quaternion.LookRotationSafe((followedLTW.Position + moveDirection.value) - followedLTW.Position, math.up());
                            transform.Rotation = Quaternion.Lerp(transform.Rotation, desiredRot, 0.001f);
                        } else {
                            transform.Rotation = quaternion.LookRotationSafe(followedLTW.Position - transform.Position, math.up());
                        }

                        float3 cameraForward = math.mul(transform.Rotation, math.forward());

                        // Distance input
                        float desiredDistanceMovementFromInput =
                            CameraInputs.Zoom * gameCamera.OrbitDistanceMovementSpeed;
                        gameCamera.OrbitTargetDistance =
                            math.clamp(gameCamera.OrbitTargetDistance + desiredDistanceMovementFromInput,
                                gameCamera.OrbitMinDistance, gameCamera.OrbitMaxDistance);
                        gameCamera.CurrentDistanceFromMovement = math.lerp(gameCamera.CurrentDistanceFromMovement,
                            gameCamera.OrbitTargetDistance,
                            MathUtilities.GetSharpnessInterpolant(gameCamera.OrbitDistanceMovementSharpness,
                                DeltaTime));

                        // Calculate final camera position from targetposition + rotation + distance
                        transform.Position = followedLTW.Position +
                                                (-cameraForward * gameCamera.CurrentDistanceFromMovement);
                    }

                    break;
                }
                case GameCamera.Mode.None:
                    break;
            }

            // Manually calculate the LocalToWorld since this is updating after the Transform systems, and the LtW is what rendering uses
            LocalToWorld cameraLocalToWorld = new LocalToWorld();
            cameraLocalToWorld.Value = new float4x4(transform.Rotation, transform.Position);
            LocalToWorldLookup[entity] = cameraLocalToWorld;
        }
    }
}
