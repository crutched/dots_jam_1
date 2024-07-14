using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct SimulationRateSystem : ISystem {
    private bool _hadFirstTimeInit;

    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<SimulationRate>();
        state.RequireForUpdate<TimeStepModification>();

        var modEnt = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData<TimeStepModification>(modEnt, new() {
            scale = 1,
        });
    }

    public void OnUpdate(ref SystemState state) {
        ref SimulationRate simRate = ref SystemAPI.GetSingletonRW<SimulationRate>().ValueRW;
        var timeMod = SystemAPI.GetSingleton<TimeStepModification>();

        if (!_hadFirstTimeInit) {
            const string _fixedRateArg = "-fixedRate:";

            // Read cmd line args
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                if (arg.Contains(_fixedRateArg)) {
                    string rate = arg.Substring(_fixedRateArg.Length);
                    if (int.TryParse(rate, out int rateInt)) {
                        if (rateInt > 0) {
                            simRate.UseFixedRate = true;
                            simRate.FixedTimeStep = 1f / (float)rateInt;
                        } else {
                            simRate.UseFixedRate = false;
                        }
                        break;
                    }
                }
            }

            _hadFirstTimeInit = true;
        }

        if (timeMod.scale != simRate.TimeScale) {
            if (math.abs(simRate.TimeScale - timeMod.scale) < 0.001f) {
                simRate.TimeScale = timeMod.scale;
            } else {
                simRate.TimeScale = math.lerp(simRate.TimeScale, timeMod.scale, 0.001f);
            }
        }

        //var currentMod = simRate.normalTimeStep * timeMod.scale;
        //if (simRate.FixedTimeStep != currentMod) {
        //    if (math.abs(simRate.FixedTimeStep - currentMod) < 0.001f) {
        //        simRate.FixedTimeStep = currentMod;
        //    } else {
        //        simRate.FixedTimeStep = math.lerp(simRate.FixedTimeStep, currentMod, 0.001f);
        //    }
        //    simRate.Update = true;
        //}

        if (simRate.Update) {
            SimulationSystemGroup simulationSystemGroup =
                state.World.GetExistingSystemManaged<SimulationSystemGroup>();

            if (simRate.UseFixedRate) {
                simulationSystemGroup.RateManager = new RateUtils.FixedRateSimpleManager(simRate.FixedTimeStep);
            } else {
                simulationSystemGroup.RateManager = null;
            }

            simRate.Update = false;
        }
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(SeedDishSystem))]
public partial struct SimulationTimeScaleSystem : ISystem {
    private bool _hadFirstTimeInit;

    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<SimulationRate>();
    }

    public void OnUpdate(ref SystemState state) {
        ref SimulationRate simRate = ref SystemAPI.GetSingletonRW<SimulationRate>().ValueRW;

        simRate.UnscaledDeltaTime = SystemAPI.Time.DeltaTime;

        state.World.SetTime(new TimeData(
            SystemAPI.Time.ElapsedTime,
            SystemAPI.Time.DeltaTime * simRate.TimeScale));
    }
}