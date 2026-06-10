namespace SearchConsoleApp.Core.Domain.PriceBenchmark;

public enum PriceBenchmarkRunStatus
{
    Pending = 0,
    Crawling = 1,
    Comparing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
}

public enum MarketPricePosition
{
    Unknown = 0,
    Below = 1,
    Average = 2,
    Above = 3,
}
