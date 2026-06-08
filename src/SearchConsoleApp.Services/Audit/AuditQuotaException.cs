namespace SearchConsoleApp.Services.Audit;

public class AuditQuotaException : Exception
{
    public AuditQuotaException(string message) : base(message) { }
}
