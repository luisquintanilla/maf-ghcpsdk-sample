using System.ComponentModel;
using System.Numerics.Tensors;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Z3;
using Plotly.NET;
using Plotly.NET.CSharp;

namespace PortfolioWorkflows;

/// <summary>
/// Optimization tool implementations using Z3 constraint solving,
/// TensorPrimitives for fast math, and Plotly.NET for charting.
/// </summary>
internal static class OptimizationTools
{
    private static readonly string MarketDataPath = Path.Combine(
        AppContext.BaseDirectory, "data", "market_data.json");

    private static readonly string ProfilePath = Path.Combine(
        AppContext.BaseDirectory, "data", "investor_profile.json");

    private record AssetData(string Symbol, double ExpectedReturn, double Volatility, double DividendYield, string Sector);
    private record ProfileData(double MaxVolatility, double MaxSinglePosition, double MaxSectorWeight, double MinBondAllocation);

    private static (List<AssetData> Assets, ProfileData Profile) LoadData()
    {
        var marketJson = JsonNode.Parse(File.ReadAllText(MarketDataPath))!;
        var profileJson = JsonNode.Parse(File.ReadAllText(ProfilePath))!;

        var assets = new List<AssetData>();
        foreach (var kvp in marketJson["assets"]!.AsObject())
        {
            var a = kvp.Value!;
            assets.Add(new AssetData(
                kvp.Key,
                a["expectedReturn"]!.GetValue<double>(),
                a["volatility"]!.GetValue<double>(),
                a["dividendYield"]!.GetValue<double>(),
                a["sector"]!.GetValue<string>()));
        }

        var profile = new ProfileData(
            profileJson["maxVolatility"]!.GetValue<double>(),
            profileJson["maxSinglePosition"]!.GetValue<double>(),
            profileJson["maxSectorWeight"]!.GetValue<double>(),
            profileJson["minBondAllocation"]!.GetValue<double>());

        return (assets, profile);
    }

    /// <summary>Runs Z3 optimizer to find optimal portfolio weights.</summary>
    [Description("Optimizes portfolio allocation using Z3 constraint solving. " +
        "Accepts risk tolerance ('conservative', 'moderate', 'aggressive') or a custom max volatility (0.0-1.0).")]
    public static string OptimizeAllocation(
        [Description("Risk level: 'conservative', 'moderate', 'aggressive', or a decimal max volatility like '0.12'")] string riskLevel = "moderate")
    {
        var (assets, profile) = LoadData();

        double maxVol = riskLevel.ToLowerInvariant() switch
        {
            "conservative" => 0.10,
            "moderate" => profile.MaxVolatility,
            "aggressive" => 0.25,
            _ => double.TryParse(riskLevel, out var custom) ? custom : profile.MaxVolatility
        };

        var result = SolveWithZ3(assets, profile, maxVol);
        return result;
    }

    /// <summary>Computes portfolio statistics using TensorPrimitives.</summary>
    [Description("Computes portfolio statistics (expected return, volatility, Sharpe ratio, sector weights) " +
        "for a given set of asset weights using TensorPrimitives for fast vector math.")]
    public static string ComputePortfolioStats(
        [Description("Comma-separated asset weights matching the 25 assets in market_data.json, e.g. '0.04,0.05,...'")] string weightsStr = "")
    {
        var (assets, _) = LoadData();
        double[] weights;

        if (string.IsNullOrWhiteSpace(weightsStr))
        {
            // Default to equal weights
            weights = new double[assets.Count];
            Array.Fill(weights, 1.0 / assets.Count);
        }
        else
        {
            weights = weightsStr.Split(',').Select(w => double.Parse(w.Trim())).ToArray();
        }

        if (weights.Length != assets.Count)
            return JsonSerializer.Serialize(new { error = $"Expected {assets.Count} weights, got {weights.Length}" });

        var returns = assets.Select(a => a.ExpectedReturn).ToArray();
        var vols = assets.Select(a => a.Volatility).ToArray();

        // TensorPrimitives for expected return: dot product of weights and returns
        double expectedReturn = TensorPrimitives.Dot(
            new ReadOnlySpan<double>(weights),
            new ReadOnlySpan<double>(returns));

        // Weighted volatility (simplified): dot product of weights and volatilities
        double[] weightedVols = new double[weights.Length];
        TensorPrimitives.Multiply(
            new ReadOnlySpan<double>(weights),
            new ReadOnlySpan<double>(vols),
            new Span<double>(weightedVols));
        double portfolioVol = TensorPrimitives.Sum(new ReadOnlySpan<double>(weightedVols));

        // Sharpe ratio with 4% risk-free rate
        double riskFreeRate = 0.04;
        double sharpe = portfolioVol > 0 ? (expectedReturn - riskFreeRate) / portfolioVol : 0;

        // Sector weights
        var sectorWeights = new Dictionary<string, double>();
        for (int i = 0; i < assets.Count; i++)
        {
            string sector = assets[i].Sector;
            if (!sectorWeights.ContainsKey(sector))
                sectorWeights[sector] = 0;
            sectorWeights[sector] += weights[i];
        }

        var result = new
        {
            expectedReturnPct = Math.Round(expectedReturn * 100, 2),
            volatilityPct = Math.Round(portfolioVol * 100, 2),
            sharpeRatio = Math.Round(sharpe, 3),
            sectorWeights = sectorWeights.ToDictionary(
                kvp => kvp.Key,
                kvp => Math.Round(kvp.Value * 100, 2))
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Renders the efficient frontier chart using Plotly.NET.</summary>
    [Description("Generates an efficient frontier chart by sweeping volatility constraints through the Z3 optimizer. " +
        "Saves an interactive HTML chart to charts/efficient-frontier.html and returns the file path.")]
    public static string RenderFrontierChart()
    {
        var (assets, profile) = LoadData();

        var frontierVols = new List<double>();
        var frontierReturns = new List<double>();

        // Sweep volatility from 5% to 30% in ~50 steps
        for (double vol = 0.05; vol <= 0.305; vol += 0.005)
        {
            var json = SolveWithZ3(assets, profile, vol);
            var node = JsonNode.Parse(json);
            if (node?["error"] != null) continue;

            double retPct = node?["expectedReturnPct"]?.GetValue<double>() ?? 0;
            double volPct = node?["volatilityPct"]?.GetValue<double>() ?? 0;
            if (retPct > 0)
            {
                frontierVols.Add(volPct);
                frontierReturns.Add(retPct);
            }
        }

        // Current portfolio point (equal-weight approximation)
        double[] equalWeights = new double[assets.Count];
        Array.Fill(equalWeights, 1.0 / assets.Count);
        var rets = assets.Select(a => a.ExpectedReturn).ToArray();
        var volsArr = assets.Select(a => a.Volatility).ToArray();
        double currentRet = TensorPrimitives.Dot(
            new ReadOnlySpan<double>(equalWeights),
            new ReadOnlySpan<double>(rets)) * 100;
        double[] wv = new double[equalWeights.Length];
        TensorPrimitives.Multiply(
            new ReadOnlySpan<double>(equalWeights),
            new ReadOnlySpan<double>(volsArr),
            new Span<double>(wv));
        double currentVol = TensorPrimitives.Sum(new ReadOnlySpan<double>(wv)) * 100;

        // Build Plotly chart
        var frontierTrace = Chart2D.Chart.Scatter<double, double, string>(
            x: frontierVols,
            y: frontierReturns,
            mode: StyleParam.Mode.Lines_Markers,
            Name: "Efficient Frontier");

        var currentTrace = Chart2D.Chart.Point<double, double, string>(
            x: new[] { currentVol },
            y: new[] { currentRet },
            Name: "Current Portfolio (Equal-Weight)");

        var combined = Plotly.NET.CSharp.Chart.Combine(new[] { frontierTrace, currentTrace })
            .WithTitle("Efficient Frontier — Portfolio Optimizer")
            .WithXAxisStyle<double, double, double>(Title: Plotly.NET.Title.init("Volatility (%)"))
            .WithYAxisStyle<double, double, double>(Title: Plotly.NET.Title.init("Expected Return (%)"));

        string chartsDir = Path.Combine(AppContext.BaseDirectory, "charts");
        Directory.CreateDirectory(chartsDir);
        string htmlPath = Path.Combine(chartsDir, "efficient-frontier.html");
        Plotly.NET.GenericChartExtensions.SaveHtml(combined, htmlPath);

        return JsonSerializer.Serialize(new { chartPath = htmlPath, points = frontierVols.Count });
    }

    private static string SolveWithZ3(List<AssetData> assets, ProfileData profile, double maxVol)
    {
        int n = assets.Count;

        using var ctx = new Context();
        var opt = ctx.MkOptimize();

        // Decision variables: weight for each asset
        var weights = new RealExpr[n];
        for (int i = 0; i < n; i++)
            weights[i] = (RealExpr)ctx.MkRealConst($"w_{assets[i].Symbol}");

        // Constraint: all weights sum to 1
        opt.Add(ctx.MkEq(
            ctx.MkAdd(weights.Select(w => (ArithExpr)w).ToArray()),
            ctx.MkReal((int)(1.0 * 1000), 1000)));

        // Constraint: 0 <= weight_i <= maxSinglePosition
        for (int i = 0; i < n; i++)
        {
            opt.Add(ctx.MkGe(weights[i], ctx.MkReal(0, 1)));
            opt.Add(ctx.MkLe(weights[i], ctx.MkReal((int)(profile.MaxSinglePosition * 10000), 10000)));
        }

        // Constraint: sector caps
        var sectorGroups = assets
            .Select((a, i) => (a.Sector, Index: i))
            .GroupBy(x => x.Sector);

        foreach (var group in sectorGroups)
        {
            var sectorSum = ctx.MkAdd(group.Select(g => (ArithExpr)weights[g.Index]).ToArray());
            opt.Add(ctx.MkLe(sectorSum, ctx.MkReal((int)(profile.MaxSectorWeight * 10000), 10000)));
        }

        // Constraint: minimum bond allocation
        var bondIndices = assets
            .Select((a, i) => (a.Sector, Index: i))
            .Where(x => x.Sector == "Bonds")
            .Select(x => x.Index);

        var bondSum = ctx.MkAdd(bondIndices.Select(i => (ArithExpr)weights[i]).ToArray());
        opt.Add(ctx.MkGe(bondSum, ctx.MkReal((int)(profile.MinBondAllocation * 10000), 10000)));

        // Constraint: simplified volatility — Σ(weight_i × vol_i) ≤ maxVol
        var volTerms = new ArithExpr[n];
        for (int i = 0; i < n; i++)
        {
            volTerms[i] = ctx.MkMul(weights[i],
                ctx.MkReal((int)(assets[i].Volatility * 10000), 10000));
        }
        opt.Add(ctx.MkLe(ctx.MkAdd(volTerms), ctx.MkReal((int)(maxVol * 10000), 10000)));

        // Objective: maximize Σ(weight_i × expectedReturn_i)
        var returnTerms = new ArithExpr[n];
        for (int i = 0; i < n; i++)
        {
            returnTerms[i] = ctx.MkMul(weights[i],
                ctx.MkReal((int)(assets[i].ExpectedReturn * 10000), 10000));
        }
        opt.MkMaximize(ctx.MkAdd(returnTerms));

        if (opt.Check() != Status.SATISFIABLE)
        {
            return JsonSerializer.Serialize(new { error = "No feasible allocation found for the given constraints." });
        }

        var model = opt.Model;
        var allocations = new List<object>();
        double totalReturn = 0;
        double totalVol = 0;
        var sectorWeights = new Dictionary<string, double>();

        for (int i = 0; i < n; i++)
        {
            var val = model.Evaluate(weights[i]);
            double w = EvalToDouble(val);

            if (w >= 0.001) // Only include allocations > 0.1%
            {
                allocations.Add(new
                {
                    symbol = assets[i].Symbol,
                    sector = assets[i].Sector,
                    weightPct = Math.Round(w * 100, 2)
                });
            }

            totalReturn += w * assets[i].ExpectedReturn;
            totalVol += w * assets[i].Volatility;

            if (!sectorWeights.ContainsKey(assets[i].Sector))
                sectorWeights[assets[i].Sector] = 0;
            sectorWeights[assets[i].Sector] += w;
        }

        var result = new
        {
            riskLevel = maxVol <= 0.10 ? "conservative" : maxVol <= 0.15 ? "moderate" : "aggressive",
            maxVolatilityConstraint = Math.Round(maxVol * 100, 1),
            allocations,
            expectedReturnPct = Math.Round(totalReturn * 100, 2),
            volatilityPct = Math.Round(totalVol * 100, 2),
            sharpeRatio = Math.Round(totalVol > 0 ? (totalReturn - 0.04) / totalVol : 0, 3),
            sectorBreakdown = sectorWeights.ToDictionary(
                kvp => kvp.Key,
                kvp => Math.Round(kvp.Value * 100, 2))
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static double EvalToDouble(Expr expr)
    {
        if (expr is RatNum ratNum)
        {
            return ratNum.Numerator.Int / (double)ratNum.Denominator.Int;
        }
        if (double.TryParse(expr.ToString(), out var d))
            return d;
        // Try fraction format "a/b"
        var parts = expr.ToString().Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0].Trim(), out var num) &&
            double.TryParse(parts[1].Trim(), out var den) &&
            den != 0)
            return num / den;
        return 0;
    }
}
