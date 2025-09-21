using NZWalks.API.Models.Domain;

namespace NZWalks.API.Models.Pagination_Result
{
    public class WalkPageResult
    {
        public List<Walk> Items { get; set; } = new List<Walk>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
    }
}
