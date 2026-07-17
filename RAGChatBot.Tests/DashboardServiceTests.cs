using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class DashboardServiceTests
{
    private readonly IDashboardRepository _dashboard = Substitute.For<IDashboardRepository>();
    private readonly IBenchmarkRepository _benchmarks = Substitute.For<IBenchmarkRepository>();

    [Fact]
    public async Task MonthChart_ReturnsAllTwelveMonths_AndFillsMissingMonthsWithZero()
    {
        _dashboard.GetActivityAsync(2026, 2026).Returns(new[]
        {
            new DashboardActivityPoint(2026, 1, 2, 3, 100m),
            new DashboardActivityPoint(2026, 1, 1, 4, 50m),
            new DashboardActivityPoint(2026, 3, 5, 6, 200m)
        });

        var result = await CreateService().GetRevenueChartAsync("Month", 2026);

        Assert.Equal(12, result.Count);
        Assert.Equal("T1", result[0].Label);
        Assert.Equal(150m, result[0].Revenue);
        Assert.Equal(3, result[0].DocumentCount);
        Assert.Equal(0m, result[1].Revenue);
    }

    [Fact]
    public async Task QuarterChart_AggregatesThreeMonthsPerQuarter()
    {
        _dashboard.GetActivityAsync(2026, 2026).Returns(new[]
        {
            new DashboardActivityPoint(2026, 1, 1, 2, 100m),
            new DashboardActivityPoint(2026, 3, 3, 4, 200m),
            new DashboardActivityPoint(2026, 4, 9, 8, 300m)
        });

        var result = await CreateService().GetRevenueChartAsync("Quarter", 2026);

        Assert.Equal(4, result.Count);
        Assert.Equal(300m, result[0].Revenue);
        Assert.Equal(4, result[0].DocumentCount);
        Assert.Equal(300m, result[1].Revenue);
    }

    [Fact]
    public async Task YearChart_RequestsAndAggregatesThreeYearWindow()
    {
        _dashboard.GetActivityAsync(2024, 2026).Returns(new[]
        {
            new DashboardActivityPoint(2024, 1, 1, 1, 10m),
            new DashboardActivityPoint(2025, 2, 2, 2, 20m),
            new DashboardActivityPoint(2026, 3, 3, 3, 30m)
        });

        var result = await CreateService().GetRevenueChartAsync("Year", 2026);

        Assert.Equal(new[] { "2024", "2025", "2026" }, result.Select(x => x.Label));
        Assert.Equal(new[] { 10m, 20m, 30m }, result.Select(x => x.Revenue));
        await _dashboard.Received(1).GetActivityAsync(2024, 2026);
    }

    private DashboardService CreateService() => new(_dashboard, _benchmarks);
}
