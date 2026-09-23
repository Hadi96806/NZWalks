using Microsoft.EntityFrameworkCore;
using NZWalks.API.Data;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.Pagination_Result;

namespace NZWalks.API.Respositries
{
    public class SQLWalkRepository : IWalkRepository
    {
        private readonly NZWalksDbContext dbContext;

        public SQLWalkRepository(NZWalksDbContext dbContext)
        {
            this.dbContext = dbContext;
        }
        public async Task<Walk> CreateAsync(Walk walk, CancellationToken cancellationToken = default)
        {
            await dbContext.AddAsync(walk, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            //Load navigations so the returned walk has its Region and Difficulty
            await dbContext.Entry(walk).Reference(w => w.Region).LoadAsync(cancellationToken);
            await dbContext.Entry(walk).Reference(w => w.Difficulty).LoadAsync(cancellationToken);
            return walk;
        }

        public async Task<Walk?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var walkToDelete = await dbContext.Walks.FindAsync(new object?[] { id }, cancellationToken);
            if (walkToDelete != null)
            {
                dbContext.Walks.Remove(walkToDelete);
                await dbContext.SaveChangesAsync(cancellationToken);
                return walkToDelete;
            }
            return null;

        }

        public async Task<WalkPageResult> GetAllAsync(string? filtertOn = null, string? filterQuery = null, string? sortBy = null, bool isAscending = true, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var walks = dbContext.Walks.AsNoTracking().Include(walks => walks.Difficulty).Include(walks => walks.Region).AsQueryable();
             //filtering
            if (!string.IsNullOrEmpty(filtertOn) && !string.IsNullOrEmpty(filterQuery))
            {
                if (filtertOn.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    walks = walks.Where(x => x.Name.Contains(filterQuery));
            }

            //sorting
            if (!string.IsNullOrEmpty(sortBy))
            {
                if (sortBy.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    walks = isAscending ? walks.OrderBy(x => x.Name) : walks.OrderByDescending(x => x.Name);
                }
                else if (sortBy.Equals("Length", StringComparison.OrdinalIgnoreCase))
                {
                    walks = isAscending ? walks.OrderBy(x => x.LengthInKm) : walks.OrderByDescending(x => x.LengthInKm);
                }
            }

            //Pagination
            var skipResults = (pageNumber - 1) * pageSize;
            var totalItems = await walks.CountAsync(cancellationToken);
            var items = await walks.Skip(skipResults).Take(pageSize).ToListAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return new WalkPageResult { 
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalRecords = totalItems
            };
            //return await dbContext.Walks.Include(w => w.Difficulty).Include(w => w.Region).ToListAsync();
        }

        public async Task<Walk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await dbContext.Walks.AsNoTracking().Include(w => w.Region).Include(w => w.Difficulty).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<Walk?> UpdateAsync(Guid id, Walk walk, CancellationToken cancellationToken = default)
        {
            var walkToUpdate = await dbContext.Walks.FindAsync(new object?[] { id }, cancellationToken);
            if (walkToUpdate == null)
            {
                return null;
            }

            walkToUpdate.Name = walk.Name;
            walkToUpdate.DifficultyId = walk.DifficultyId;
            walkToUpdate.RegionId = walk.RegionId;
            walkToUpdate.Description = walk.Description;
            walkToUpdate.LengthInKm = walk.LengthInKm;
            walkToUpdate.WalkImageUrl = walk.WalkImageUrl;

            await dbContext.SaveChangesAsync(cancellationToken);
            walkToUpdate = await dbContext.Walks.Include(w => w.Region).Include(walk => walk.Difficulty).FirstOrDefaultAsync(walk => walk.Id == id, cancellationToken);

            return walkToUpdate;

        }
    }
}
