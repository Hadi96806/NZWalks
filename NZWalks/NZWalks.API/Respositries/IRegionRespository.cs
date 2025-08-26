using NZWalks.API.Models.Domain;

namespace NZWalks.API.Respositries
{
    public interface IRegionRespository
    {
        Task<List<Region>> GetAllAsync();
        Task<Region?> GetByIdAsync(Guid id);
        Task<Region> CreateAsync(Region region);
        Task<Region> UpdateAsync(Guid id,Region region);
        Task<Region> DeleteAsync(Guid id);         
    }
}
