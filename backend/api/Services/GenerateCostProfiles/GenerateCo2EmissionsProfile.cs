using api.Adapters;
using api.Context;
using api.Dtos;
using api.Helpers;
using api.Models;

using AutoMapper;

namespace api.Services.GenerateCostProfiles;

public class GenerateCo2EmissionsProfile : IGenerateCo2EmissionsProfile
{
    private readonly ICaseService _caseService;
    private readonly IDrainageStrategyService _drainageStrategyService;
    private readonly IProjectService _projectService;
    private readonly ITopsideService _topsideService;
    private readonly IWellProjectService _wellProjectService;
    private readonly IWellProjectWellService _wellProjectWellService;
    private readonly DcdDbContext _context;
    private readonly IMapper _mapper;

    public GenerateCo2EmissionsProfile(
        DcdDbContext context,
        ICaseService caseService,
        IDrainageStrategyService drainageStrategyService,
        IProjectService projectService,
        ITopsideService topsideService,
        IWellProjectService wellProjectService,
        IWellProjectWellService wellProjectWellService,
        IMapper mapper
        )
    {
        _context = context;
        _caseService = caseService;
        _projectService = projectService;
        _topsideService = topsideService;
        _drainageStrategyService = drainageStrategyService;
        _wellProjectService = wellProjectService;
        _wellProjectWellService = wellProjectWellService;
        _mapper = mapper;
    }

    public async Task<Co2EmissionsDto> GenerateAsync(Guid caseId)
    {
        var caseItem = await _caseService.GetCase(caseId);
        var topside = await _topsideService.GetTopside(caseItem.TopsideLink);
        var project = await _projectService.GetProjectWithoutAssets(caseItem.ProjectId);
        var drainageStrategy = await _drainageStrategyService.GetDrainageStrategy(caseItem.DrainageStrategyLink);
        var wellProject = await _wellProjectService.GetWellProject(caseItem.WellProjectLink);

        var fuelConsumptionsProfile = GetFuelConsumptionsProfile(project, caseItem, topside, drainageStrategy);
        var flaringsProfile = GetFlaringsProfile(project, drainageStrategy);
        var lossesProfile = GetLossesProfile(project, drainageStrategy);

        var tempProfile = TimeSeriesCost.MergeCostProfilesList(new List<TimeSeries<double>> { fuelConsumptionsProfile, flaringsProfile, lossesProfile });

        var convertedValues = tempProfile.Values.Select(v => v / 1000);

        var newProfile = new TimeSeries<double>
        {
            StartYear = tempProfile.StartYear,
            Values = convertedValues.ToArray(),
        };

        var drillingEmissionsProfile = await CalculateDrillingEmissions(project, wellProject);

        var totalProfile =
            TimeSeriesCost.MergeCostProfiles(newProfile, drillingEmissionsProfile);
        var co2Emission = drainageStrategy.Co2Emissions ?? new Co2Emissions();

        co2Emission.StartYear = totalProfile.StartYear;
        co2Emission.Values = totalProfile.Values;

        await UpdateDrainageStrategyAndSaveAsync(drainageStrategy, co2Emission);

        var dto = _mapper.Map<Co2EmissionsDto>(co2Emission);

        return dto ?? new Co2EmissionsDto();
    }

    private async Task<int> UpdateDrainageStrategyAndSaveAsync(DrainageStrategy drainageStrategy, Co2Emissions co2Emissions)
    {
        drainageStrategy.Co2Emissions = co2Emissions;
        return await _context.SaveChangesAsync();
    }

    private static TimeSeriesVolume GetLossesProfile(Project project, DrainageStrategy drainageStrategy)
    {
        var losses = EmissionCalculationHelper.CalculateLosses(project, drainageStrategy);

        var lossesProfile = new TimeSeriesVolume
        {
            StartYear = losses.StartYear,
            Values = losses.Values.Select(loss => loss * project.CO2Vented).ToArray(),
        };
        return lossesProfile;
    }

    private static TimeSeriesVolume GetFlaringsProfile(Project project, DrainageStrategy drainageStrategy)
    {
        var flarings = EmissionCalculationHelper.CalculateFlaring(project, drainageStrategy);

        var flaringsProfile = new TimeSeriesVolume
        {
            StartYear = flarings.StartYear,
            Values = flarings.Values.Select(flare => flare * project.CO2EmissionsFromFlaredGas).ToArray(),
        };
        return flaringsProfile;
    }

    private static TimeSeriesVolume GetFuelConsumptionsProfile(Project project, Case caseItem, Topside topside,
        DrainageStrategy drainageStrategy)
    {
        var fuelConsumptions =
            EmissionCalculationHelper.CalculateTotalFuelConsumptions(caseItem, topside, drainageStrategy);

        var fuelConsumptionsProfile = new TimeSeriesVolume
        {
            StartYear = fuelConsumptions.StartYear,
            Values = fuelConsumptions.Values.Select(fuel => fuel * project.CO2EmissionFromFuelGas).ToArray(),
        };
        return fuelConsumptionsProfile;
    }

    private async Task<TimeSeriesVolume> CalculateDrillingEmissions(Project project, WellProject wellProject)
    {
        var linkedWells = await _wellProjectWellService.GetWellProjectWellsForWellProject(wellProject.Id);
        if (linkedWells == null)
        {
            return new TimeSeriesVolume();
        }

        var wellDrillingSchedules = new TimeSeries<double>();
        foreach (var linkedWell in linkedWells)
        {
            if (linkedWell.DrillingSchedule == null)
            {
                continue;
            }

            var timeSeries = new TimeSeries<double>
            {
                StartYear = linkedWell.DrillingSchedule.StartYear,
                Values = linkedWell.DrillingSchedule.Values.Select(v => (double)v).ToArray(),
            };
            wellDrillingSchedules = TimeSeriesCost.MergeCostProfiles(wellDrillingSchedules, timeSeries);
        }

        var drillingEmission = new ProductionProfileGas
        {
            StartYear = wellDrillingSchedules.StartYear,
            Values = wellDrillingSchedules.Values
                .Select(well => well * project.AverageDevelopmentDrillingDays * project.DailyEmissionFromDrillingRig)
                .ToArray(),
        };

        return drillingEmission;
    }
}
