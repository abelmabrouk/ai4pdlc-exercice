using Conges.Application.Common;

namespace Conges.Application.LeaveRequests;

/// <summary>
/// Codes d'erreur des demandes de congé : liste fermée tenue dans le wiki (page « Codes d'erreur »).
/// Un code absent du référentiel n'existe pas.
/// </summary>
public static class LeaveErrors
{
    public const string InvalidPeriodCode = "LEAVE_INVALID_PERIOD";
    public const string NoticeTooShortCode = "LEAVE_NOTICE_TOO_SHORT";
    public const string InsufficientBalanceCode = "LEAVE_INSUFFICIENT_BALANCE";
    public const string OverlapCode = "LEAVE_OVERLAP";
    public const string RejectionReasonRequiredCode = "LEAVE_REJECTION_REASON_REQUIRED";
    public const string ApproverNotAllowedCode = "LEAVE_APPROVER_NOT_ALLOWED";
    public const string AlreadyDecidedCode = "LEAVE_ALREADY_DECIDED";
    public const string NotFoundCode = "LEAVE_NOT_FOUND";

    /// <summary>RG-01, RG-02.</summary>
    public static BusinessException InvalidPeriod(string detail) =>
        new(InvalidPeriodCode, BusinessErrorKind.RuleViolation, "Période de congé invalide.", detail);

    /// <summary>RG-03.</summary>
    public static BusinessException NoticeTooShort() =>
        new(NoticeTooShortCode, BusinessErrorKind.RuleViolation, "Délai de prévenance non respecté.",
            "Au moins 2 jours ouvrés doivent séparer la soumission du premier jour de congé.");

    /// <summary>RG-05, RG-12.</summary>
    public static BusinessException InsufficientBalance(int requested, int available) =>
        new(InsufficientBalanceCode, BusinessErrorKind.RuleViolation, "Solde de congés insuffisant.",
            $"{requested} jours demandés, {available} jours disponibles.");

    /// <summary>RG-06.</summary>
    public static BusinessException Overlap() =>
        new(OverlapCode, BusinessErrorKind.RuleViolation, "Chevauchement avec une autre demande.",
            "La période chevauche une demande en attente ou approuvée.");

    /// <summary>RG-13.</summary>
    public static BusinessException RejectionReasonRequired() =>
        new(RejectionReasonRequiredCode, BusinessErrorKind.RuleViolation, "Motif de refus obligatoire.",
            "Le motif du refus doit contenir au moins 10 caractères.");

    /// <summary>RG-10, circuit de validation.</summary>
    public static BusinessException ApproverNotAllowed(string detail) =>
        new(ApproverNotAllowedCode, BusinessErrorKind.Forbidden, "Utilisateur non habilité.", detail);

    /// <summary>RG-11.</summary>
    public static BusinessException AlreadyDecided() =>
        new(AlreadyDecidedCode, BusinessErrorKind.Conflict, "Demande déjà décidée.",
            "La demande a déjà été approuvée ou refusée.");

    public static BusinessException NotFound(int id) =>
        new(NotFoundCode, BusinessErrorKind.NotFound, "Demande de congé introuvable.",
            $"Demande de congé '{id}' introuvable.");
}
