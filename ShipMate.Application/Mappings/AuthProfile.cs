using AutoMapper;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Domain.Entities;

namespace ShipMate.Application.Mappings;

public class AuthProfile : Profile
{
    public AuthProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));
    }
}
