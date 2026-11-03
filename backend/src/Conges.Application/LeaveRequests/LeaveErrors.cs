using Conges.Application.Common;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Erreurs métier des demandes de congé. Chaque code est tiré du référentiel fermé du wiki (« Codes d'erreur »).
/// </summary>
public static class LeaveErrors
{
    public static BusinessRuleException InvalidPeriod() =>
        new("LEAVE_INVALID_PERIOD", BusinessErrorKind.RuleViolation, "Période de congé invalide.");

    public static BusinessRuleException NoticeTooShort() =>
        new("LEAVE_NOTICE_TOO_SHORT", BusinessErrorKind.RuleViolation, "Délai de prévenance non respecté.");

    public static BusinessRuleException InsufficientBalance(int requested, int available) =>
        new("LEAVE_INSUFFICIENT_BALANCE", BusinessErrorKind.RuleViolation, "Solde de congés insuffisant.",
            $"{requested} jours demandés, {available} jours disponibles.");

    public static BusinessRuleException Overlap() =>
        new("LEAVE_OVERLAP", BusinessErrorKind.RuleViolation, "Chevauchement avec une autre demande.");

    public static BusinessRuleException RejectionReasonRequired() =>
        new("LEAVE_REJECTION_REASON_REQUIRED", BusinessErrorKind.RuleViolation, "Motif de refus absent ou trop court.");

    public static BusinessRuleException ApproverNotAllowed() =>
        new("LEAVE_APPROVER_NOT_ALLOWED", BusinessErrorKind.Forbidden, "Vous n'êtes pas le valideur de cette demande.");

    public static BusinessRuleException AlreadyDecided() =>
        new("LEAVE_ALREADY_DECIDED", BusinessErrorKind.Conflict, "La demande a déjà été décidée.");

    public static BusinessRuleException NotFound(int id) =>
        new("LEAVE_NOT_FOUND", BusinessErrorKind.NotFound, "Demande de congé introuvable.",
            $"Demande de congé '{id}' introuvable.");
}
