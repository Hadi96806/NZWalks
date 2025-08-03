using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Data;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegionsController : ControllerBase
    {
        private readonly NZWalksDbContext dbContext;

        public RegionsController(NZWalksDbContext _context)
        {
                this.dbContext = _context;
        }

        //Get All Regions
        [HttpGet]
        public IActionResult GetAll()
        {
            var regions = dbContext.Regions.ToList();
            var regionsDto = new List<RegionDto>();
            foreach (var item in regions)
            {
                regionsDto.Add(new RegionDto()
                {
                    Id = item.Id,
                    Name = item.Name,
                    Code = item.Code,
                    RegionImageUrl = item. RegionImageUrl
                });
            }
            return Ok(regionsDto);
        }

        //Get Region By Id
        [HttpGet]
        [Route("id:Guid")]
        public IActionResult GetById(Guid id) {
            //var region = dbContext.Regions.FirstOrDefault(x=>x.Id==id);
            var region = dbContext.Regions.Find(id);
            if (region == null)
            {
                return NotFound();
            }
            var regionDto = new RegionDto() { 
                Id = region.Id,
                Name = region.Name,
                Code = region.Code,
                RegionImageUrl = region.RegionImageUrl
            };
            return Ok(regionDto); 
        }

        //Create New Region
        [HttpPost]
        public IActionResult Create(AddRegionDto addRegionDto) {
            var regionDomainModal = new Region()
            {
                Name = addRegionDto.Name,
                Code = addRegionDto.Code,
                RegionImageUrl = addRegionDto.RegionImageUrl
            };

            dbContext.Regions.Add(regionDomainModal);
            dbContext.SaveChanges();

            var regionDto = new RegionDto()
            {
                Id = regionDomainModal.Id,
                Name = regionDomainModal.Name,
                Code = regionDomainModal.Code,
                RegionImageUrl = regionDomainModal.RegionImageUrl
            };

            return CreatedAtAction(nameof(GetById), new { id = regionDto.Id }, regionDto);
        }

        //Update Region
        [HttpPut]
        [Route("{id:Guid}")]
        public IActionResult Update([FromRoute] Guid id, [FromBody] UpdateRegionDto updateRegionDto) 
        { 
             var regionDomainModel = dbContext.Regions.FirstOrDefault(region => region.Id == id);

            if(regionDomainModel == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(updateRegionDto?.Name))
            {
                regionDomainModel.Name = updateRegionDto.Name;
            }

            if (!string.IsNullOrWhiteSpace(updateRegionDto?.Code))
            {
                regionDomainModel.Code = updateRegionDto.Code;
            }

            if (!string.IsNullOrWhiteSpace(updateRegionDto?.RegionImageUrl))
            {
                regionDomainModel.RegionImageUrl = updateRegionDto.RegionImageUrl;
            }


            dbContext.SaveChanges();

            var regionDto = new RegionDto {
                Id = regionDomainModel.Id,
                Name = regionDomainModel?.Name,
                Code = regionDomainModel?.Code,
                RegionImageUrl = regionDomainModel?.RegionImageUrl
            };

            return Ok(regionDto);
        }

        //Delete Region
        [HttpDelete]
        [Route("{id:Guid}")]
        public IActionResult Delete([FromRoute] Guid id)
        {
            var regionDomainModel =  dbContext.Regions.FirstOrDefault(region =>region.Id == id);
            if(regionDomainModel == null)
            {
                return NotFound();
            }

            dbContext.Regions.Remove(regionDomainModel);
            dbContext.SaveChanges();
            var regionDto = new RegionDto
            {
                Id = regionDomainModel.Id,
                Name = regionDomainModel.Name,
                Code = regionDomainModel.Code,
                RegionImageUrl = regionDomainModel.RegionImageUrl
            };
            return Ok(regionDto);
        }
    }
}
