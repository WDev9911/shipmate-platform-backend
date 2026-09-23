using AutoMapper;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Domain.Entities;

namespace ShipMate.Application.Mappings;

public class WorkspaceProfile : Profile
{
    public WorkspaceProfile()
    {
        CreateMap<Workspace, WorkspaceDto>();

        CreateMap<WorkspaceMember, WorkspaceMemberDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.User.DisplayName));

        CreateMap<WorkspaceMember, WorkspaceInvitationDto>()
            .ForMember(dest => dest.WorkspaceName, opt => opt.MapFrom(src => src.Workspace.Name))
            .ForMember(dest => dest.InvitedAt, opt => opt.MapFrom(src => src.CreatedAt));
    }
}
