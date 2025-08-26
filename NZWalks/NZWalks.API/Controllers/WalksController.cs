using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;
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
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var walks = await walkRepository.GetAllAsync();

            // Map walkDomainModal to walkDto
            var walksDto = mapper.Map<List<WalkDto>>(walks);

            return Ok(walksDto);
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
    }
}
