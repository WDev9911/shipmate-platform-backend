using AutoMapper;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Domain.Entities;

namespace ShipMate.Application.Mappings;

public class AdminProfile : Profile
{
    public AdminProfile()
    {
        CreateMap<User, AdminUserListItemDto>();
    }
}
