using NZWalks.API.Models.Domain;
using NZWalks.API.Models.Pagination_Result;

namespace NZWalks.API.Respositries
{
    public interface IWalkRepository
    {
        Task<WalkPageResult> GetAllAsync(string? filtertOn = null, string? filrQuery = null, string? sortBy = null, bool isAscending = true,
            int pageNumber = 1, int pageSize = 10);
        Task<Walk> GetByIdAsync(Guid id);
        Task<Walk> CreateAsync(Walk walk);
        Task<Walk?> UpdateAsync(Guid id, Walk walk);
        Task<Walk> DeleteAsync(Guid id);
    }
}
