namespace SearchConsoleApp.Core.Domain.Audit;

public enum AuditRunStatus
{
    Pending = 0,
    Crawling = 1,
    Analyzing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum AuditMode
{
    Anonymous = 0,
    Connected = 1
}

public enum AuditIssueSeverity
{
    Critical = 0,
    Warning = 1,
    Info = 2
}

public enum AuditIssueSource
{
    Crawl = 0,
    PageSpeed = 1,
    SearchConsole = 2,
    Serp = 3,
    Llm = 4,
    SafeBrowsing = 5,
    System = 6
}
