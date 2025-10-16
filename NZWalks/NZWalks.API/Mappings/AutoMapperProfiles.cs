using AutoMapper;
using NZWalks.API.Models.Domain;
using NZWalks.API.Models.DTO;
using NZWalks.API.Models.Pagination_Result;

namespace NZWalks.API.Mappings
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<Region, RegionDtoV1>().ReverseMap();
            CreateMap<Region, RegionDtoV2>()
                .ForMember(dest => dest.HasImage,
                           opt => opt.MapFrom(src => string.IsNullOrEmpty(src.RegionImageUrl)))
                .ReverseMap();
            CreateMap<Region, UpdateRegionDto>().ReverseMap();
            CreateMap<Region, AddRegionDto>().ReverseMap();
            CreateMap<Walk, AddWalkDto>().ReverseMap();
            CreateMap<Walk, WalkDto>().ReverseMap();
            CreateMap<Walk, UpdateWalkDto>().ReverseMap();
            CreateMap<Difficulty, DifficultyDto>().ReverseMap();
            CreateMap<WalkPageDtoResult, WalkPageResult>().ReverseMap();
        }
    }
}
