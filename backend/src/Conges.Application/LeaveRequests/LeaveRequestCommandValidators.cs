using FluentValidation;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Validation de forme uniquement (400). Les règles de gestion (RG-01 à RG-13) sont dans
/// <see cref="LeaveRequestCommands"/> et produisent un code métier (ADR-003).
/// </summary>
internal sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequestDto>
{
    public SubmitLeaveRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

internal sealed class RejectLeaveRequestValidator : AbstractValidator<RejectLeaveRequestDto>
{
    public RejectLeaveRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
