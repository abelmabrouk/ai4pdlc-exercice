namespace Conges.Application.Common;

/// <summary>Nature d'une erreur métier ; l'API en déduit le statut HTTP (ADR-003).</summary>
public enum ErrorKind
{
    /// <summary>Règle de gestion non respectée.</summary>
    RuleViolation,

    /// <summary>Action interdite à cet utilisateur.</summary>
    Forbidden,

    /// <summary>Ressource absente.</summary>
    NotFound,

    /// <summary>Conflit avec l'état actuel de la ressource.</summary>
    Conflict,
}

public sealed record LeaveError(string Code, string Title, ErrorKind Kind);

/// <summary>
/// Référentiel fermé des codes d'erreur (wiki « Codes d'erreur ») : un code absent de cette liste n'existe pas.
/// </summary>
public static class LeaveErrors
{
    public static readonly LeaveError InvalidPeriod = new("LEAVE_INVALID_PERIOD", "Période de congé invalide.", ErrorKind.RuleViolation);
    public static readonly LeaveError NoticeTooShort = new("LEAVE_NOTICE_TOO_SHORT", "Délai de prévenance non respecté.", ErrorKind.RuleViolation);
    public static readonly LeaveError InsufficientBalance = new("LEAVE_INSUFFICIENT_BALANCE", "Solde de congés insuffisant.", ErrorKind.RuleViolation);
    public static readonly LeaveError Overlap = new("LEAVE_OVERLAP", "Chevauchement avec une autre demande.", ErrorKind.RuleViolation);
    public static readonly LeaveError RejectionReasonRequired = new("LEAVE_REJECTION_REASON_REQUIRED", "Motif de refus requis.", ErrorKind.RuleViolation);
    public static readonly LeaveError ApproverNotAllowed = new("LEAVE_APPROVER_NOT_ALLOWED", "Utilisateur non habilité à décider de cette demande.", ErrorKind.Forbidden);
    public static readonly LeaveError AlreadyDecided = new("LEAVE_ALREADY_DECIDED", "Demande déjà décidée.", ErrorKind.Conflict);
    public static readonly LeaveError NotFound = new("LEAVE_NOT_FOUND", "Demande de congé introuvable.", ErrorKind.NotFound);
}

/// <summary>Règle de gestion non respectée, porteuse d'un code du référentiel.</summary>
public sealed class BusinessRuleException(LeaveError error, string detail) : Exception(detail)
{
    public LeaveError Error { get; } = error;
}
