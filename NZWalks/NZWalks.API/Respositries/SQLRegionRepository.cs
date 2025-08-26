using Microsoft.EntityFrameworkCore;
using NZWalks.API.Data;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Respositries
{
    public class SQLRegionRepository : IRegionRespository
    {
        public NZWalksDbContext dbContext { get; }

        public SQLRegionRepository(NZWalksDbContext nZWalksDbContext)
        {
            dbContext = nZWalksDbContext;
        }

        public async Task<List<Region>> GetAllAsync()
        {
            return await dbContext.Regions.ToListAsync();
        }

        public async Task<Region> CreateAsync(Region region)
        {
            await dbContext.Regions.AddAsync(region);
            await dbContext.SaveChangesAsync();
            return region;
        }

        public async Task<Region> DeleteAsync(Guid id)
        {
            var regionToDelete = await dbContext.Regions.FindAsync(id);
            if(regionToDelete != null)
            {
                dbContext.Regions.Remove(regionToDelete);
                await dbContext.SaveChangesAsync();
                return regionToDelete;
            }
            return null;

        }

        public async Task<Region?> GetByIdAsync(Guid id)
        {
            return await dbContext.Regions.FindAsync(id);
        }

        public async Task<Region> UpdateAsync(Guid id, Region region)
        {
            var regionToUpdate = await dbContext.Regions.FindAsync(id);
            if(regionToUpdate == null)
            {
                return null;
            }

            if(!string.IsNullOrEmpty(region.Name))
            {
                regionToUpdate.Name = region.Name;
            }

            if(!string.IsNullOrEmpty(region.Code))
            {
                regionToUpdate.Code = region.Code;
            }

            if (!string.IsNullOrEmpty(region.RegionImageUrl))
            {
                regionToUpdate.RegionImageUrl = region.RegionImageUrl;
            }

            dbContext.SaveChangesAsync();
            return regionToUpdate;
        }
    }
}
