using System.ComponentModel.DataAnnotations.Schema;

namespace NZWalks.API.Models.Domain
{
    public class Image
    {
        public Guid id { get; set; }
        [NotMapped]
        //Not stored in the database; the controller sets it from the validated upload
        public IFormFile File { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string FileExtension { get; set; } = string.Empty;
        public long FileSizInBytes { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}
