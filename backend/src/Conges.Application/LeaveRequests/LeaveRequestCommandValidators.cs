using FluentValidation;

namespace Conges.Application.LeaveRequests;

internal sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequestDto>
{
    public SubmitLeaveRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("La date de fin doit être postérieure ou égale à la date de début.");
        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(time.GetLocalNow().DateTime))
            .WithMessage("La date de début ne peut pas être dans le passé.");
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

internal sealed class RejectLeaveRequestValidator : AbstractValidator<RejectLeaveRequestDto>
{
    public RejectLeaveRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithMessage("Le motif de refus est obligatoire.")
            .MaximumLength(500);
    }
}
