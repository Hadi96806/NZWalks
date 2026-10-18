using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NZWalks.API.Constants;
using NZWalks.API.Data;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;
using NZWalks.API.Repositories;
using System.Text.Json;

namespace NZWalks.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion(1.0)]
    [ApiVersion(2.0)]
    public class RegionsController : ControllerBase
    {
        private readonly IRegionRepository regionRepository;
        private readonly IMapper mapper;

        public ILogger<RegionsController> Logger { get; }

        public RegionsController(IRegionRepository regionRepository, IMapper mapper, ILogger<RegionsController> logger)
        {
            this.regionRepository = regionRepository;
            this.mapper = mapper;
            Logger = logger;
        }

        //Get All Regions V1
        [HttpGet]
        [Authorize(Policy = PolicyNames.CanRead)]
        [MapToApiVersion("1.0")]
        public async Task<IActionResult> GetAllV1(CancellationToken cancellationToken)
        {
            Logger.LogInformation("GET All Regions Action Invoked");
            var regions = await regionRepository.GetAllAsync(cancellationToken);

            //Map Domina Model To DTO
            var regionsDto = mapper.Map<List<RegionDtoV1>>(regions);
            Logger.LogInformation($"Get All Regions Finished {regionsDto.Count} regions was retrieved");
            Logger.LogInformation($"Regions : {JsonSerializer.Serialize(regionsDto)}");
            return Ok(regionsDto);
        }

        //Get All Regions v2
        [HttpGet]
        [Authorize(Policy = PolicyNames.CanRead)]
        [MapToApiVersion("2.0")]
        public async Task<IActionResult> GetAllV2(CancellationToken cancellationToken)
        {
            Logger.LogInformation("GET All Regions Action Invoked");
            var regions = await regionRepository.GetAllAsync(cancellationToken);

            //Map Domina Model To DTO
            var regionsDto = mapper.Map<List<RegionDtoV2>>(regions);
            Logger.LogInformation($"Get All Regions Finished {regionsDto.Count} regions was retrieved");
            Logger.LogInformation($"Regions : {JsonSerializer.Serialize(regionsDto)}");
            return Ok(regionsDto);
        }

        //Get Region By Id
        [HttpGet]
        [Route("{id:Guid}")]
        [Authorize(Policy = PolicyNames.CanRead)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            //var region = dbContext.Regions.FirstOrDefault(x=>x.Id==id);
            var region = await regionRepository.GetByIdAsync(id, cancellationToken);
            if (region == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Region with id {id} was not found.");
            }
            var regionDto = mapper.Map<RegionDtoV1>(region);
            return Ok(regionDto);
        }

        //Create New Region
        [HttpPost]
        [Authorize(Policy = PolicyNames.CanWrite)]
        public async Task<IActionResult> Create(AddRegionDto addRegionDto, CancellationToken cancellationToken)
        {
            var regionDomainModal = mapper.Map<Region>(addRegionDto);

            await regionRepository.CreateAsync(regionDomainModal, cancellationToken);

            var regionDto = mapper.Map<RegionDtoV1>(regionDomainModal);

            return CreatedAtAction(nameof(GetById), new { id = regionDto.Id }, regionDto);
        }

        //Update Region
        [HttpPut]
        [Route("{id:Guid}")]
        [Authorize(Policy = PolicyNames.CanWrite)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateRegionDto updateRegionDto, CancellationToken cancellationToken)
        {
            var updateRegion = mapper.Map<Region>(updateRegionDto);

            var regionDomainModel = await regionRepository.UpdateAsync(id, updateRegion, cancellationToken);
            if (regionDomainModel == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Region with id {id} was not found.");
            }

            var regionDto = mapper.Map<RegionDtoV1>(regionDomainModel);

            return Ok(regionDto);
        }

        //Delete Region
        [HttpDelete]
        [Route("{id:Guid}")]
        [Authorize(Policy = PolicyNames.CanWrite)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var regionDomainModel = await regionRepository.DeleteAsync(id, cancellationToken);
            if (regionDomainModel == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Region with id {id} was not found.");
            }

            var regionDto = mapper?.Map<RegionDtoV1>(regionDomainModel);
            return Ok(regionDto);
        }
    }
}
