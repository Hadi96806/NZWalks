using NZWalks.API.Models.Domain;

namespace NZWalks.API.Respositries
{
    public interface IImageRepository
    {
        Task<Image> Upload(Image image);
    }
}
