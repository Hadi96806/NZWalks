using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NZWalks.API.CustomActionFilter;
using NZWalks.API.Data;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;
using NZWalks.API.Respositries;

namespace NZWalks.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegionsController : ControllerBase
    {
        private readonly IRegionRespository regionRepositry;
        private readonly IMapper mapper;

        public RegionsController(IRegionRespository regionRepositry, IMapper mapper)
        {
            this.regionRepositry = regionRepositry;
            this.mapper = mapper;
        }

        //Get All Regions
        [HttpGet]
        [Authorize(Roles="Reader,Admin")]
        public async Task<IActionResult> GetAll()
        {
            var regions = await regionRepositry.GetAllAsync();

            //Map Domina Model To DTO
            var regionsDto = mapper.Map<List<RegionDto>>(regions);
            
            return Ok(regionsDto);
        }

        //Get Region By Id
        [HttpGet]
        [Route("id:Guid")]
        [Authorize(Roles ="Reader,Admin")]
        public async Task<IActionResult> GetById(Guid id) {
            //var region = dbContext.Regions.FirstOrDefault(x=>x.Id==id);
            var region = await regionRepositry.GetByIdAsync(id);
            if (region == null)
            {
                return NotFound();
            }
            var regionDto = mapper.Map<RegionDto>(region);
            return Ok(regionDto); 
        }

        //Create New Region
        [HttpPost]
        [ValidateModel]
        [Authorize(Roles = "Writer,Admin")]
        public async Task<IActionResult> Create(AddRegionDto addRegionDto) {
            if(ModelState.IsValid)
            {
                var regionDomainModal = mapper.Map<Region>(addRegionDto);

                await regionRepositry.CreateAsync(regionDomainModal);

                var regionDto = mapper.Map<RegionDto>(regionDomainModal);

                return CreatedAtAction(nameof(GetById), new { id = regionDto.Id }, regionDto);
            }
            else
            {
                return BadRequest();
            }
        }

        //Update Region
        [HttpPut]
        [Route("{id:Guid}")]
        [ValidateModel]
        [Authorize(Roles = "Writer,Admin")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateRegionDto updateRegionDto)
        {
            var updateRegion = mapper.Map<Region>(updateRegionDto);

            var regionDomainModel = await regionRepositry.UpdateAsync(id, updateRegion);
            if (regionDomainModel == null)
            {
                return NotFound();
            }

            var regionDto = mapper.Map<RegionDto>(regionDomainModel);

            return Ok(regionDto);
        }

        //Delete Region
        [HttpDelete]
        [Route("{id:Guid}")]
        [Authorize(Roles = "Writer,Admin")]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var regionDomainModel = await regionRepositry.DeleteAsync(id);
            if(regionDomainModel == null)
            {
                return NotFound();
            }
         
            var regionDto = mapper?.Map<RegionDto>(regionDomainModel);
            return Ok(regionDto);
        }
    }
}
