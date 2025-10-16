using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;
using NZWalks.API.Respositries;
using System.Net.NetworkInformation;

namespace NZWalks.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {

        public ImagesController(IImageRepository imageRepository)
        {
            ImageRepository = imageRepository;
        }

        public IImageRepository ImageRepository { get; }

        // POST: /api/Images/Upload
        [HttpPost]
        [Route("Upload")]
        public async Task<IActionResult> Upload([FromForm] ImageUploadDto img)
        {
            ValidateFileUpload(img);

            if(ModelState.IsValid)
             {
                //Convert Dto to DomainModel
                var imageDomainModel = new Image
                {
                    File = img.File,
                    Name = img.Name,
                    Description = img.Description,
                    FileExtension = Path.GetExtension(img.File.FileName),
                    FileSizInBytes = img.File.Length,
                };

                //Use repository to upload image
                await ImageRepository.Upload(imageDomainModel);

                return Ok(imageDomainModel);
            }
            return BadRequest(ModelState);
        }

        private void ValidateFileUpload(ImageUploadDto img)
        {
            var allowedExtensions = new string[] { ".jpg", ".png", ".gif", ".svg", ".webp" };
            if (!allowedExtensions.Contains(Path.GetExtension(img.File.FileName)))
            {
                ModelState.AddModelError("File", "Unsupported file extension");
            }
            if (img.File.Length > 10485760)
            {
                ModelState.AddModelError("File", "File size exceeds 10MB.");
            }

        }
    }
}
