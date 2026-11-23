using AutoMapper;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Mappings;

public class WorkspaceProfile : Profile
{
    public WorkspaceProfile()
    {
        CreateMap<Workspace, WorkspaceDto>();

        CreateMap<WorkspaceMember, WorkspaceMemberDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.User.DisplayName))
            .ForMember(dest => dest.AccountLocked, opt => opt.MapFrom(src => src.User != null && src.User.Status != UserStatus.Active))
            .ForMember(dest => dest.RepoInvite, opt => opt.Ignore());

        CreateMap<WorkspaceMember, WorkspaceInvitationDto>()
            .ForMember(dest => dest.WorkspaceName, opt => opt.MapFrom(src => src.Workspace.Name))
            .ForMember(dest => dest.InvitedAt, opt => opt.MapFrom(src => src.CreatedAt));
    }
}
