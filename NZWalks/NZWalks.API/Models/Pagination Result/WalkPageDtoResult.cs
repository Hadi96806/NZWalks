using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;

namespace NZWalks.API.Models.Pagination_Result
{
    public class WalkPageDtoResult
    {
        public List<WalkDto> Items { get; set; } = new List<WalkDto>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
    }
}
