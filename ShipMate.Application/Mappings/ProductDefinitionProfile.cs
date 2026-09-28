using AutoMapper;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Entities;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Application.Mappings;

public class ProductDefinitionProfile : Profile
{
    public ProductDefinitionProfile()
    {
        CreateMap<ProductDefinition, ProductDefinitionDto>()
            .ForMember(dest => dest.Features, opt => opt.MapFrom(src => src.Features.OrderBy(f => f.Position)));

        CreateMap<LockedPersona, LockedPersonaDto>();
        CreateMap<SupportingRole, SupportingRoleDto>();

        CreateMap<Feature, FeatureDto>()
            .ForMember(dest => dest.AiAssessment, opt => opt.MapFrom(src => src.AiVerdict == null
                ? null
                : new AiAssessmentDto { Verdict = src.AiVerdict.Value, Reason = src.AiReason }))
            .ForMember(dest => dest.DependsOn, opt => opt.MapFrom(src => src.Dependencies));

        CreateMap<FeatureDependency, FeatureDependencyDto>();
    }
}
