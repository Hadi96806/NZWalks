using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Constants;
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
        //Upload writes to disk and the Images table, so it is a write: Writer/Admin (D6). Downloads under /Images stay public.
        [Authorize(Roles = $"{RoleNames.Writer},{RoleNames.Admin}")]
        public async Task<IActionResult> Upload([FromForm] ImageUploadDto img, CancellationToken cancellationToken)
        {
            //File type, size and fields are checked by ImageUploadDtoValidator before this runs
            var imageDomainModel = new Image
            {
                File = img.File,
                Name = img.Name,
                Description = img.Description,
                FileExtension = Path.GetExtension(img.File.FileName),
                FileSizInBytes = img.File.Length,
            };

            //Use repository to upload image
            await ImageRepository.Upload(imageDomainModel, cancellationToken);

            return Ok(imageDomainModel);
        }
    }
}
