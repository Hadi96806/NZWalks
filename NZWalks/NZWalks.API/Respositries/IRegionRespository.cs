using NZWalks.API.Models.Domain;

namespace NZWalks.API.Respositries
{
    public interface IRegionRespository
    {
        Task<List<Region>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Region?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Region> CreateAsync(Region region, CancellationToken cancellationToken = default);
        Task<Region?> UpdateAsync(Guid id, Region region, CancellationToken cancellationToken = default);
        Task<Region?> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
