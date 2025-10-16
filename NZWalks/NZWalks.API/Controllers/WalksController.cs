using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.CustomActionFilter;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;
using NZWalks.API.Models.Pagination_Result;
using NZWalks.API.Respositries;

namespace NZWalks.API.Controllers
{
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
        public async Task<IActionResult> GetAll([FromQuery] string? filtertOn, [FromQuery] string? filrQuery,[FromQuery] string? sortBy,
            [FromQuery] bool? isAscending, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var walkPageResult = await walkRepository.GetAllAsync(filtertOn, filrQuery, sortBy, isAscending ??  true, pageNumber, pageSize) ;
            
            // Map walkDomainModal to walkDto
            var walkPageDtoResult = mapper.Map<WalkPageDtoResult>(walkPageResult);
            return Ok(walkPageDtoResult);
        }

        //Get Walk by id
        [HttpGet]
        [Route("{id:Guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var walk = await walkRepository.GetByIdAsync(id);

            if(walk == null)
            {
                return NotFound();
            }
            //Map WalkDomainModal to walkDto
            var walkDto = mapper.Map<WalkDto>(walk);

            return Ok(walkDto);

        }

        //Create Walk
        [HttpPost]
        [ValidateModel]
        public async Task<IActionResult> Create([FromBody] AddWalkDto addWalkDto)
        {
            //Map Dto to Domain Model
            var walkDomainModal = mapper.Map<Walk>(addWalkDto);

            var walkDto = await walkRepository.CreateAsync(walkDomainModal);

            return CreatedAtAction( nameof(GetById), new { Id = walkDto.Id } ,walkDto); 
        }

        // Update Walk
        [HttpPut]
        [Route("{id:Guid}")]
        [ValidateModel]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateWalkDto updateWalkDto)
        {
            //Map Dto to Domainmodal
            var walkDomainModal = mapper.Map<Walk>(updateWalkDto);

            var updatedWalk = await walkRepository.UpdateAsync(id, walkDomainModal);

            if(updatedWalk == null)
                return NotFound();

            var updatedWalkDto = mapper.Map<WalkDto>(updatedWalk);

            return Ok(updatedWalkDto);
        }

        // Delete Walk
        [HttpDelete]
        [Route("{id:guid}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var walkToDelete = await walkRepository.DeleteAsync(id);
            if(walkToDelete == null)
                return NotFound();

            var waltDto = mapper.Map<WalkDto>(walkToDelete);
            return Ok(waltDto);
        }
    }
}
