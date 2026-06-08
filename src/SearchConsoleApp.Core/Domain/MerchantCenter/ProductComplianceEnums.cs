namespace SearchConsoleApp.Core.Domain.MerchantCenter;

public enum ProductComplianceRunStatus
{
    Pending = 0,
    Crawling = 1,
    Analyzing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
}

public enum ProductComplianceAnalysisMode
{
    SiteOnly = 0,
    GmcConnected = 1,
}

public enum ProductComplianceItemStatus
{
    Compliant = 0,
    Partial = 1,
    NonCompliant = 2,
}

public enum ProductComplianceIssueSource
{
    SpecValidation = 0,
    MerchantCenter = 1,
    SiteLevel = 2,
    CrossProduct = 3,
    PageSpeed = 4,
    SafeBrowsing = 5,
}

public enum ProductComplianceIssueSeverity
{
    Critical = 0,
    Warning = 1,
    Info = 2,
}
