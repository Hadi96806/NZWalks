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

        public async Task<List<Region>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await dbContext.Regions.AsNoTracking().ToListAsync(cancellationToken);
        }

        public async Task<Region> CreateAsync(Region region, CancellationToken cancellationToken = default)
        {
            await dbContext.Regions.AddAsync(region, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return region;
        }

        public async Task<Region?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var regionToDelete = await dbContext.Regions.FindAsync(new object?[] { id }, cancellationToken);
            if(regionToDelete != null)
            {
                dbContext.Regions.Remove(regionToDelete);
                await dbContext.SaveChangesAsync(cancellationToken);
                return regionToDelete;
            }
            return null;

        }

        public async Task<Region?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            //FindAsync always tracks, so query by key instead
            return await dbContext.Regions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<Region?> UpdateAsync(Guid id, Region region, CancellationToken cancellationToken = default)
        {
            var regionToUpdate = await dbContext.Regions.FindAsync(new object?[] { id }, cancellationToken);
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

            await dbContext.SaveChangesAsync(cancellationToken);
            return regionToUpdate;
        }
    }
}
