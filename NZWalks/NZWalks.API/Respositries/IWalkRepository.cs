using NZWalks.API.Models.Domain;
using NZWalks.API.Models.Pagination_Result;

namespace NZWalks.API.Respositries
{
    public interface IWalkRepository
    {
        Task<WalkPageResult> GetAllAsync(string? filtertOn = null, string? filrQuery = null, string? sortBy = null, bool isAscending = true,
            int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default);
        Task<Walk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Walk> CreateAsync(Walk walk, CancellationToken cancellationToken = default);
        Task<Walk?> UpdateAsync(Guid id, Walk walk, CancellationToken cancellationToken = default);
        Task<Walk?> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
