namespace NZWalks.API.Models.DTO
{
    //Query string for GET /api/Walks — a class so FluentValidation auto-validation can run on it
    public class WalkQueryDto
    {
        public string? FilterOn { get; set; }
        public string? FilterQuery { get; set; }
        public string? SortBy { get; set; }
        public bool? IsAscending { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
