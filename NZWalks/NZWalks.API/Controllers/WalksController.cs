using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Constants;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;
using NZWalks.API.Models.Pagination_Result;
using NZWalks.API.Respositries;

namespace NZWalks.API.Controllers
{
    //Same split as RegionsController (D6): Reader/Admin read, Writer/Admin change
    [Route("api/[controller]")]
    [ApiController]
    public class WalksController : ControllerBase
    {
        private readonly IMapper mapper;
        private readonly IWalkRepository walkRepository;

        public WalksController(IMapper mapper, IWalkRepository walkRepository)
        {
            this.mapper = mapper;
            this.walkRepository = walkRepository;
        }

        //Get All Walks
        //GET: /api/walk?filterOn=Name&filterQuery=track&sortBy=length&isAscending=true&pageNumber=1&pageSize=10
        [HttpGet]
        [Authorize(Policy = PolicyNames.CanRead)]
        public async Task<IActionResult> GetAll([FromQuery] WalkQueryDto query, CancellationToken cancellationToken)
        {
            var walkPageResult = await walkRepository.GetAllAsync(query.FilterOn, query.FilterQuery, query.SortBy, query.IsAscending ?? true,
                query.PageNumber, query.PageSize, cancellationToken);

            // Map walkDomainModal to walkDto
            var walkPageDtoResult = mapper.Map<WalkPageDtoResult>(walkPageResult);
            return Ok(walkPageDtoResult);
        }

        //Get Walk by id
        [HttpGet]
        [Route("{id:Guid}")]
        [Authorize(Policy = PolicyNames.CanRead)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var walk = await walkRepository.GetByIdAsync(id, cancellationToken);

            if (walk == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Walk with id {id} was not found.");
            }
            //Map WalkDomainModal to walkDto
            var walkDto = mapper.Map<WalkDto>(walk);

            return Ok(walkDto);

        }

        //Create Walk
        [HttpPost]
        [Authorize(Policy = PolicyNames.CanWrite)]
        public async Task<IActionResult> Create([FromBody] AddWalkDto addWalkDto, CancellationToken cancellationToken)
        {
            //Map Dto to Domain Model
            var walkDomainModal = mapper.Map<Walk>(addWalkDto);

            var walkDto = await walkRepository.CreateAsync(walkDomainModal, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { Id = walkDto.Id }, walkDto);
        }

        // Update Walk
        [HttpPut]
        [Route("{id:Guid}")]
        [Authorize(Policy = PolicyNames.CanWrite)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateWalkDto updateWalkDto, CancellationToken cancellationToken)
        {
            //Map Dto to Domainmodal
            var walkDomainModal = mapper.Map<Walk>(updateWalkDto);

            var updatedWalk = await walkRepository.UpdateAsync(id, walkDomainModal, cancellationToken);

            if (updatedWalk == null)
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Walk with id {id} was not found.");

            var updatedWalkDto = mapper.Map<WalkDto>(updatedWalk);

            return Ok(updatedWalkDto);
        }

        // Delete Walk
        [HttpDelete]
        [Route("{id:guid}")]
        [Authorize(Policy = PolicyNames.CanWrite)]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var walkToDelete = await walkRepository.DeleteAsync(id, cancellationToken);
            if (walkToDelete == null)
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: $"Walk with id {id} was not found.");

            var waltDto = mapper.Map<WalkDto>(walkToDelete);
            return Ok(waltDto);
        }
    }
}
