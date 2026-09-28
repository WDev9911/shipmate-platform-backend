using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Constants;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class AnalyzeProductDefinitionRequestValidator : AbstractValidator<AnalyzeProductDefinitionRequest>
{
    public AnalyzeProductDefinitionRequestValidator()
    {
        RuleFor(x => x.Instruction)
            .MaximumLength(AiAnalysisRunConstraints.InstructionMaxLength)
            .When(x => x.Instruction is not null);
    }
}
