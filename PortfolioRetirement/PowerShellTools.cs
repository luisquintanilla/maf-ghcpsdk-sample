using System.ComponentModel;
using System.Management.Automation;

namespace PortfolioRetirement;

/// <summary>
/// Tool implementations that use in-process PowerShell pipelines to analyse
/// the mock portfolio CSV.  Each method spins up a short-lived PowerShell
/// instance, runs a pipeline, and returns the JSON result string.
/// </summary>
internal static class PowerShellTools
{
    private static readonly string CsvPath = Path.Combine(
        AppContext.BaseDirectory, "data", "holdings.csv");

    /// <summary>Returns total portfolio value, cost basis, gain/loss, and holding count.</summary>
    [Description("Returns a summary of the portfolio: total value, cost basis, total gain/loss, and number of holdings")]
    public static string GetPortfolioSummary()
    {
        using var ps = PowerShell.Create();
        ps.AddScript($@"
            $holdings = Import-Csv '{CsvPath}'
            $summary = $holdings | ForEach-Object {{
                [PSCustomObject]@{{
                    Symbol       = $_.Symbol
                    CurrentValue = [double]$_.Shares * [double]$_.CurrentPrice
                    CostBasis    = [double]$_.Shares * [double]$_.PurchasePrice
                }}
            }}
            $totalValue = ($summary | Measure-Object -Property CurrentValue -Sum).Sum
            $totalCost  = ($summary | Measure-Object -Property CostBasis    -Sum).Sum
            [PSCustomObject]@{{
                TotalValue   = [math]::Round($totalValue, 2)
                CostBasis    = [math]::Round($totalCost, 2)
                GainLoss     = [math]::Round($totalValue - $totalCost, 2)
                GainLossPct  = [math]::Round((($totalValue - $totalCost) / $totalCost) * 100, 2)
                HoldingCount = $holdings.Count
            }} | ConvertTo-Json
        ");
        return InvokeAndReturn(ps);
    }

    /// <summary>Groups holdings by sector with weight percentages.</summary>
    [Description("Returns a breakdown of the portfolio by sector, showing value and weight percentage for each sector")]
    public static string GetSectorBreakdown()
    {
        using var ps = PowerShell.Create();
        ps.AddScript($@"
            $holdings = Import-Csv '{CsvPath}'
            $withValue = $holdings | ForEach-Object {{
                [PSCustomObject]@{{
                    Sector = $_.Sector
                    Value  = [double]$_.Shares * [double]$_.CurrentPrice
                }}
            }}
            $total = ($withValue | Measure-Object -Property Value -Sum).Sum
            $withValue | Group-Object Sector | ForEach-Object {{
                $sectorValue = ($_.Group | Measure-Object -Property Value -Sum).Sum
                [PSCustomObject]@{{
                    Sector    = $_.Name
                    Value     = [math]::Round($sectorValue, 2)
                    WeightPct = [math]::Round(($sectorValue / $total) * 100, 2)
                    Holdings  = $_.Count
                }}
            }} | Sort-Object Value -Descending | ConvertTo-Json
        ");
        return InvokeAndReturn(ps);
    }

    /// <summary>Returns the top holdings by current market value.</summary>
    [Description("Returns the top N holdings by current market value, with gain/loss for each")]
    public static string GetTopHoldings(
        [Description("Number of top holdings to return")] int count = 5)
    {
        using var ps = PowerShell.Create();
        ps.AddScript($@"
            $holdings = Import-Csv '{CsvPath}'
            $holdings | ForEach-Object {{
                $currentVal = [double]$_.Shares * [double]$_.CurrentPrice
                $costBasis  = [double]$_.Shares * [double]$_.PurchasePrice
                [PSCustomObject]@{{
                    Symbol       = $_.Symbol
                    Name         = $_.Name
                    Sector       = $_.Sector
                    CurrentValue = [math]::Round($currentVal, 2)
                    GainLoss     = [math]::Round($currentVal - $costBasis, 2)
                    GainLossPct  = [math]::Round((($currentVal - $costBasis) / $costBasis) * 100, 2)
                    AccountType  = $_.AccountType
                }}
            }} | Sort-Object CurrentValue -Descending | Select-Object -First {count} | ConvertTo-Json
        ");
        return InvokeAndReturn(ps);
    }

    private static string InvokeAndReturn(PowerShell ps)
    {
        var results = ps.Invoke();

        if (ps.HadErrors)
        {
            var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
            return $"{{\"error\": \"{errors}\"}}";
        }

        return results.Count > 0
            ? results[0].BaseObject.ToString() ?? "{}"
            : "{}";
    }
}
