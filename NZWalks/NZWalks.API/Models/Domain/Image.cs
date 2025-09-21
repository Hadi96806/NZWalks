using System.ComponentModel.DataAnnotations.Schema;

namespace NZWalks.API.Models.Domain
{
    public class Image
    {
        public Guid id { get; set; }
        [NotMapped]
        public IFormFile File { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string FileExtension { get; set; }
        public long FileSizInBytes { get; set; }
        public string FilePath { get; set; }
    }
}
