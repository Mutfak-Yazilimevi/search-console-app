namespace SearchConsoleApp.Services.Privacy;

/// <summary>
/// GDPR / KVKK compliance:
/// - Right to be forgotten (silme talebi)
/// - Data export (kullanıcının kendi verisini indirme)
///
/// Stratejik karar: **anonymize, sil değil**.
/// - Customer ham silinirse → audit log'larda orphan referans
/// - Foreign key cascade ile audit log silinirse → hukuki uyum kaybolur
///   (ne yaptığının kanıtı kayboluyor)
/// - Soft delete + PII anonymization → KVKK/GDPR uyumlu (kişi tanımlanamaz),
///   audit history korunur
///
/// Anonymize edilenler:
/// - Customer: Email, FirstName, LastName, Username → "deleted-{Id}@anonymized"
/// - DeviceSession: IpAddress, UserAgent → null
/// - AuditLog: ActorEmail, ActorIp, ActorUserAgent → null (action ve target kalır)
///
/// Korunanlar (denetim için):
/// - Customer.Id, Customer.CreatedOnUtc — soft delete ile işaretli
/// - AuditLog.Action, AuditLog.TargetType — "ne yapıldı" hukuki kanıt
/// </summary>
public interface IGdprService
{
    /// <summary>
    /// Customer verisini export et (JSON). Kullanıcı kendi datasını isteyebilir.
    /// Customer + AuditLog + DeviceSession + ExternalLogin dahil.
    /// </summary>
    Task<string> ExportCustomerDataAsync(long customerId);

    /// <summary>
    /// "Right to be forgotten" — PII anonymize, soft-delete işaretle.
    /// Audit log'a "gdpr.delete" kaydı düşer (kim talep etti, ne zaman).
    /// </summary>
    Task AnonymizeCustomerAsync(long customerId, string reason);
}
