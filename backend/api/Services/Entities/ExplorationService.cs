using api.Context;
using api.Dtos;
using api.Exceptions;
using api.Models;
using api.Repositories;

using AutoMapper;

using Microsoft.EntityFrameworkCore;

namespace api.Services;

public class ExplorationService : IExplorationService
{
    private readonly DcdDbContext _context;
    private readonly IProjectService _projectService;

    private readonly ILogger<ExplorationService> _logger;
    private readonly IMapper _mapper;
    private readonly ICaseRepository _caseRepository;
    private readonly IExplorationRepository _repository;
    private readonly IMapperService _mapperService;

    public ExplorationService(
        DcdDbContext context,
        IProjectService projectService,
        ILoggerFactory loggerFactory,
        IMapper mapper,
        ICaseRepository caseRepository,
        IExplorationRepository repository,
        IMapperService mapperService
        )
    {
        _context = context;
        _projectService = projectService;
        _logger = loggerFactory.CreateLogger<ExplorationService>();
        _mapper = mapper;
        _caseRepository = caseRepository;
        _repository = repository;
        _mapperService = mapperService;
    }


    public async Task<ExplorationWithProfilesDto> CopyExploration(Guid explorationId, Guid sourceCaseId)
    {
        var source = await GetExploration(explorationId);
        var newExplorationDto = _mapper.Map<ExplorationWithProfilesDto>(source);
        if (newExplorationDto == null)
        {
            throw new ArgumentNullException(nameof(newExplorationDto));
        }
        newExplorationDto.Id = Guid.Empty;

        if (newExplorationDto.ExplorationWellCostProfile != null)
        {
            newExplorationDto.ExplorationWellCostProfile.Id = Guid.Empty;
        }
        if (newExplorationDto.AppraisalWellCostProfile != null)
        {
            newExplorationDto.AppraisalWellCostProfile.Id = Guid.Empty;
        }
        if (newExplorationDto.SidetrackCostProfile != null)
        {
            newExplorationDto.SidetrackCostProfile.Id = Guid.Empty;
        }
        if (newExplorationDto.SeismicAcquisitionAndProcessing != null)
        {
            newExplorationDto.SeismicAcquisitionAndProcessing.Id = Guid.Empty;
        }
        if (newExplorationDto.CountryOfficeCost != null)
        {
            newExplorationDto.CountryOfficeCost.Id = Guid.Empty;
        }
        if (newExplorationDto.GAndGAdminCost != null)
        {
            newExplorationDto.GAndGAdminCost.Id = Guid.Empty;
        }

        // var wellProject = await NewCreateExploration(newExplorationDto, sourceCaseId);
        // var dto = ExplorationDtoAdapter.Convert(wellProject);
        // return dto;
        return newExplorationDto;
    }

    public async Task<Exploration> CreateExploration(Guid projectId, Guid sourceCaseId, CreateExplorationDto explorationDto)
    {
        var exploration = _mapper.Map<Exploration>(explorationDto);
        if (exploration == null)
        {
            throw new ArgumentNullException(nameof(exploration));
        }
        var project = await _projectService.GetProject(projectId);
        exploration.Project = project;
        var createdExploration = _context.Explorations!.Add(exploration);
        await _context.SaveChangesAsync();
        await SetCaseLink(exploration, sourceCaseId, project);
        return createdExploration.Entity;
    }

    private async Task SetCaseLink(Exploration exploration, Guid sourceCaseId, Project project)
    {
        var case_ = project.Cases!.FirstOrDefault(o => o.Id == sourceCaseId);
        if (case_ == null)
        {
            throw new NotFoundInDBException(string.Format("Case {0} not found in database.", sourceCaseId));
        }
        case_.ExplorationLink = exploration.Id;
        await _context.SaveChangesAsync();
    }

    public async Task<ExplorationWithProfilesDto> UpdateExplorationAndCostProfiles(ExplorationWithProfilesDto updatedExplorationDto)
    {
        var existing = await GetExploration(updatedExplorationDto.Id);
        _mapper.Map(updatedExplorationDto, existing);

        var updatedExploration = _context.Explorations!.Update(existing);
        await _context.SaveChangesAsync();
        var explorationDto = _mapper.Map<ExplorationWithProfilesDto>(updatedExploration.Entity);
        if (explorationDto == null)
        {
            throw new ArgumentNullException(nameof(explorationDto));
        }
        return explorationDto;
    }

    public async Task<Exploration> GetExploration(Guid explorationId)
    {
        var exploration = await _context.Explorations!
            .Include(c => c.ExplorationWellCostProfile)
            .Include(c => c.AppraisalWellCostProfile)
            .Include(c => c.SidetrackCostProfile)
            .Include(c => c.GAndGAdminCost)
            .Include(c => c.SeismicAcquisitionAndProcessing)
            .Include(c => c.CountryOfficeCost)
            .FirstOrDefaultAsync(o => o.Id == explorationId);
        if (exploration == null)
        {
            throw new ArgumentException(string.Format("Exploration {0} not found.", explorationId));
        }
        return exploration;
    }

    public async Task<ExplorationDto> UpdateExploration(
        Guid caseId,
        Guid explorationId,
        UpdateExplorationDto updatedExplorationDto
    )
    {
        var existingExploration = await _repository.GetExploration(explorationId)
            ?? throw new NotFoundInDBException($"Exploration with id {explorationId} not found.");

        _mapperService.MapToEntity(updatedExplorationDto, existingExploration, explorationId);

        Exploration updatedExploration;
        try
        {
            updatedExploration = await _repository.UpdateExploration(existingExploration);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to update exploration with id {explorationId} for case id {caseId}.", explorationId, caseId);
            throw;
        }

        await _caseRepository.UpdateModifyTime(caseId);

        var dto = _mapperService.MapToDto<Exploration, ExplorationDto>(updatedExploration, explorationId);
        return dto;
    }

    public async Task<ExplorationWellDto> UpdateExplorationWell(
        Guid caseId,
        Guid explorationId,
        Guid wellId,
        UpdateExplorationWellDto updatedExplorationWellDto
    )
    {
        var existingExplorationWell = await _repository.GetExplorationWell(explorationId, wellId)
            ?? throw new NotFoundInDBException($"Exploration with id {explorationId} not found.");

        _mapperService.MapToEntity(updatedExplorationWellDto, existingExplorationWell, explorationId);

        ExplorationWell updatedExplorationWell;
        try
        {
            updatedExplorationWell = await _repository.UpdateExplorationWell(existingExplorationWell);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to update exploration with id {explorationId} and well id {wellId}.", explorationId, wellId);
            throw;
        }

        await _caseRepository.UpdateModifyTime(caseId);

        var dto = _mapperService.MapToDto<ExplorationWell, ExplorationWellDto>(updatedExplorationWell, explorationId);
        return dto;
    }

    public async Task<SeismicAcquisitionAndProcessingDto> UpdateSeismicAcquisitionAndProcessing(
        Guid caseId,
        Guid wellProjectId,
        Guid profileId,
        UpdateSeismicAcquisitionAndProcessingDto updateDto
    )
    {
        return await UpdateExplorationCostProfile<SeismicAcquisitionAndProcessing, SeismicAcquisitionAndProcessingDto, UpdateSeismicAcquisitionAndProcessingDto>(
            caseId,
            wellProjectId,
            profileId,
            updateDto,
            _repository.GetSeismicAcquisitionAndProcessing,
            _repository.UpdateSeismicAcquisitionAndProcessing
        );
    }

    public async Task<CountryOfficeCostDto> UpdateCountryOfficeCost(
        Guid caseId,
        Guid wellProjectId,
        Guid profileId,
        UpdateCountryOfficeCostDto updateDto
    )
    {
        return await UpdateExplorationCostProfile<CountryOfficeCost, CountryOfficeCostDto, UpdateCountryOfficeCostDto>(
            caseId,
            wellProjectId,
            profileId,
            updateDto,
            _repository.GetCountryOfficeCost,
            _repository.UpdateCountryOfficeCost
        );
    }

    private async Task<TDto> UpdateExplorationCostProfile<TProfile, TDto, TUpdateDto>(
        Guid caseId,
        Guid explorationId,
        Guid profileId,
        TUpdateDto updatedProfileDto,
        Func<Guid, Task<TProfile?>> getProfile,
        Func<TProfile, Task<TProfile>> updateProfile
    )
        where TProfile : class, IExplorationTimeSeries
        where TDto : class
        where TUpdateDto : class
    {
        var existingProfile = await getProfile(profileId)
            ?? throw new NotFoundInDBException($"Cost profile with id {profileId} not found.");

        _mapperService.MapToEntity(updatedProfileDto, existingProfile, explorationId);

        TProfile updatedProfile;
        try
        {
            updatedProfile = await updateProfile(existingProfile);
        }
        catch (DbUpdateException ex)
        {
            var profileName = typeof(TProfile).Name;
            _logger.LogError(ex, "Failed to update profile {profileName} with id {profileId} for case id {caseId}.", profileName, profileId, caseId);
            throw;
        }

        await _caseRepository.UpdateModifyTime(caseId);

        var updatedDto = _mapperService.MapToDto<TProfile, TDto>(updatedProfile, profileId);
        return updatedDto;
    }
}
