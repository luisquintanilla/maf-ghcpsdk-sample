using System.ComponentModel;
using System.Globalization;
using System.Numerics.Tensors;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Z3;
using Plotly.NET.CSharp;
using static Plotly.NET.StyleParam;

namespace PortfolioWorkflows;

/// <summary>
/// Tax optimization tools using Z3 constraint solving, TensorPrimitives, and Plotly.NET.
/// </summary>
internal static class TaxTools
{
    private static readonly string ProjectDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    private static readonly string ChartsDir = InitDir("charts");

    private static string InitDir(string name)
    {
        var dir = Path.Combine(ProjectDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    // ── Data models ──────────────────────────────────────────────────────────

    private record Holding(string Symbol, string Name, string Sector, double Shares,
        double PurchasePrice, double CurrentPrice, string AccountType);

    private record TaxLot(int LotId, string Symbol, DateTime PurchaseDate, double Shares,
        double PurchasePrice, double CurrentPrice, string AccountType);

    private record AssetInfo(double ExpectedReturn, double Volatility, double DividendYield, string Sector);

    // ── File loaders ─────────────────────────────────────────────────────────

    private static List<Holding> LoadHoldings()
    {
        var lines = File.ReadAllLines(Path.Combine(DataDir, "holdings.csv")).Skip(1);
        return lines.Select(line =>
        {
            var p = line.Split(',');
            return new Holding(p[0], p[1], p[2],
                double.Parse(p[3], CultureInfo.InvariantCulture),
                double.Parse(p[4], CultureInfo.InvariantCulture),
                double.Parse(p[5], CultureInfo.InvariantCulture),
                p[6]);
        }).ToList();
    }

    private static List<TaxLot> LoadTaxLots()
    {
        var lines = File.ReadAllLines(Path.Combine(DataDir, "tax_lots.csv")).Skip(1);
        return lines.Select(line =>
        {
            var p = line.Split(',');
            return new TaxLot(
                int.Parse(p[0], CultureInfo.InvariantCulture),
                p[1],
                DateTime.Parse(p[2], CultureInfo.InvariantCulture),
                double.Parse(p[3], CultureInfo.InvariantCulture),
                double.Parse(p[4], CultureInfo.InvariantCulture),
                double.Parse(p[5], CultureInfo.InvariantCulture),
                p[6]);
        }).ToList();
    }

    private static Dictionary<string, AssetInfo> LoadMarketData()
    {
        var json = File.ReadAllText(Path.Combine(DataDir, "market_data.json"));
        var root = JsonNode.Parse(json)!;
        var assets = root["assets"]!.AsObject();
        var result = new Dictionary<string, AssetInfo>();
        foreach (var (symbol, node) in assets)
        {
            var a = node!;
            result[symbol] = new AssetInfo(
                a["expectedReturn"]!.GetValue<double>(),
                a["volatility"]!.GetValue<double>(),
                a["dividendYield"]!.GetValue<double>(),
                a["sector"]!.GetValue<string>());
        }
        return result;
    }

    private static JsonNode LoadInvestorProfile()
    {
        var json = File.ReadAllText(Path.Combine(DataDir, "investor_profile.json"));
        return JsonNode.Parse(json)!;
    }

    // ── Helper: extract double from Z3 model value ───────────────────────────

    private static double EvalDouble(Model model, Expr expr)
    {
        var val = model.Evaluate(expr, true);
        if (val is RatNum ratNum)
        {
            double num = ratNum.Numerator.Int;
            double den = ratNum.Denominator.Int;
            return num / den;
        }
        // Handle fraction strings like "1/2"
        var s = val.ToString();
        if (s.Contains('/'))
        {
            var parts = s.Split('/');
            return double.Parse(parts[0], CultureInfo.InvariantCulture) /
                   double.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        return double.Parse(s, CultureInfo.InvariantCulture);
    }

    // ── Tool 1: Optimize Asset Location (Z3 assignment CSP) ──────────────────

    [Description("Uses Z3 to recommend optimal account type for each holding to minimize tax drag")]
    public static string OptimizeAssetLocation()
    {
        var holdings = LoadHoldings();
        var marketData = LoadMarketData();
        var profile = LoadInvestorProfile();

        double taxableBal = profile["accounts"]!["taxable"]!["balance"]!.GetValue<double>();
        double rothBal = profile["accounts"]!["roth"]!["balance"]!.GetValue<double>();
        double tradBal = profile["accounts"]!["traditional"]!["balance"]!.GetValue<double>();

        string[] accountNames = ["Taxable", "Roth", "Traditional"];

        using var ctx = new Context();
        var opt = ctx.MkOptimize();

        int n = holdings.Count;
        var accountVars = new IntExpr[n];
        var values = new double[n];
        var dividendYields = new double[n];
        var expectedReturns = new double[n];
        var isBond = new bool[n];

        for (int i = 0; i < n; i++)
        {
            var h = holdings[i];
            accountVars[i] = ctx.MkIntConst($"account_{i}");
            values[i] = h.Shares * h.CurrentPrice;

            // Constrain to {0, 1, 2}
            opt.Add(ctx.MkGe(accountVars[i], ctx.MkInt(0)));
            opt.Add(ctx.MkLe(accountVars[i], ctx.MkInt(2)));

            if (marketData.TryGetValue(h.Symbol, out var info))
            {
                dividendYields[i] = info.DividendYield;
                expectedReturns[i] = info.ExpectedReturn;
                isBond[i] = info.Sector == "Bonds";
            }
        }

        // Account balance constraints using scaled integers
        int scale = 100; // scale dollars to cents
        for (int acct = 0; acct < 3; acct++)
        {
            double balance = acct switch { 0 => taxableBal, 1 => rothBal, _ => tradBal };
            var terms = new ArithExpr[n];
            for (int i = 0; i < n; i++)
            {
                int valueCents = (int)(values[i] * scale);
                terms[i] = (ArithExpr)ctx.MkITE(
                    ctx.MkEq(accountVars[i], ctx.MkInt(acct)),
                    ctx.MkInt(valueCents),
                    ctx.MkInt(0));
            }
            opt.Add(ctx.MkLe(ctx.MkAdd(terms), ctx.MkInt((int)(balance * scale))));
        }

        // Objective: minimize tax drag score
        var scoreTerms = new ArithExpr[n];
        for (int i = 0; i < n; i++)
        {
            int divScore = (int)(dividendYields[i] * 10000);
            int growthScore = (int)(expectedReturns[i] * 10000);
            int bondBonus = isBond[i] ? 500 : 0;

            var taxableCost = ctx.MkInt(divScore + bondBonus);
            var rothCost = ctx.MkInt(Math.Max(0, divScore - growthScore));
            var tradCost = ctx.MkInt(Math.Max(0, -divScore + growthScore - bondBonus));

            scoreTerms[i] = (ArithExpr)ctx.MkITE(
                ctx.MkEq(accountVars[i], ctx.MkInt(0)), taxableCost,
                (ArithExpr)ctx.MkITE(
                    ctx.MkEq(accountVars[i], ctx.MkInt(1)), rothCost,
                    tradCost));
        }

        opt.MkMinimize(ctx.MkAdd(scoreTerms));

        var results = new List<object>();
        double currentDrag = 0;
        double optimizedDrag = 0;

        if (opt.Check() == Status.SATISFIABLE)
        {
            var model = opt.Model;
            for (int i = 0; i < n; i++)
            {
                var val = model.Evaluate(accountVars[i], true);
                int acctIdx = int.Parse(val.ToString());
                string recommended = accountNames[acctIdx];
                string current = holdings[i].AccountType;

                double annualDividends = values[i] * dividendYields[i];
                double currentTax = current == "Taxable" ? annualDividends * 0.22 : 0;
                double optimizedTax = recommended == "Taxable" ? annualDividends * 0.22 : 0;
                currentDrag += currentTax;
                optimizedDrag += optimizedTax;

                results.Add(new
                {
                    symbol = holdings[i].Symbol,
                    name = holdings[i].Name,
                    value = Math.Round(values[i], 2),
                    currentAccount = current,
                    recommendedAccount = recommended,
                    changed = current != recommended,
                    annualDividends = Math.Round(annualDividends, 2),
                    taxSaved = Math.Round(currentTax - optimizedTax, 2)
                });
            }
        }

        var response = new
        {
            recommendations = results,
            summary = new
            {
                currentAnnualTaxDrag = Math.Round(currentDrag, 2),
                optimizedAnnualTaxDrag = Math.Round(optimizedDrag, 2),
                estimatedAnnualSavings = Math.Round(currentDrag - optimizedDrag, 2)
            }
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── Tool 2: Find Harvest Candidates (Z3 selection w/ wash sale) ──────────

    [Description("Uses Z3 to find tax-loss harvesting candidates while respecting wash sale rules. Pass 0 to maximize.")]
    public static string FindHarvestCandidates(
        [Description("Target harvest amount in dollars, or 0 to maximize")] double targetAmount = 0)
    {
        var lots = LoadTaxLots();
        var today = DateTime.Today;

        // Filter to Taxable lots with losses
        var lossingLots = lots
            .Where(l => l.AccountType == "Taxable" && l.CurrentPrice < l.PurchasePrice)
            .ToList();

        if (lossingLots.Count == 0)
            return JsonSerializer.Serialize(new { message = "No taxable lots with losses found.", candidates = Array.Empty<object>() });

        using var ctx = new Context();
        var opt = ctx.MkOptimize();

        int m = lossingLots.Count;
        var sellVars = new BoolExpr[m];
        var losses = new double[m];

        for (int i = 0; i < m; i++)
        {
            sellVars[i] = ctx.MkBoolConst($"sell_{i}");
            losses[i] = (lossingLots[i].PurchasePrice - lossingLots[i].CurrentPrice) * lossingLots[i].Shares;
        }

        // Wash sale constraints
        var washSaleWarnings = new List<object>();
        for (int i = 0; i < m; i++)
        {
            var lot = lossingLots[i];
            bool hasRecentPurchase = lots.Any(other =>
                other.Symbol == lot.Symbol &&
                other.LotId != lot.LotId &&
                Math.Abs((other.PurchaseDate - today).TotalDays) <= 30);

            if (hasRecentPurchase)
            {
                opt.Add(ctx.MkNot(sellVars[i]));
                washSaleWarnings.Add(new
                {
                    lotId = lot.LotId,
                    symbol = lot.Symbol,
                    warning = $"Wash sale risk: another lot of {lot.Symbol} was purchased within 30 days of today"
                });
            }
        }

        // Target constraint
        if (targetAmount > 0)
        {
            int targetCents = (int)(targetAmount * 100);
            var harvestTerms = new ArithExpr[m];
            for (int i = 0; i < m; i++)
            {
                int lossCents = (int)(losses[i] * 100);
                harvestTerms[i] = (ArithExpr)ctx.MkITE(sellVars[i], ctx.MkInt(lossCents), ctx.MkInt(0));
            }
            opt.Add(ctx.MkGe(ctx.MkAdd(harvestTerms), ctx.MkInt(targetCents)));
        }

        // Objective: maximize total harvested losses
        {
            var objTerms = new ArithExpr[m];
            for (int i = 0; i < m; i++)
            {
                int lossCents = (int)(losses[i] * 100);
                objTerms[i] = (ArithExpr)ctx.MkITE(sellVars[i], ctx.MkInt(lossCents), ctx.MkInt(0));
            }
            opt.MkMaximize(ctx.MkAdd(objTerms));
        }

        var candidates = new List<object>();
        double totalHarvest = 0;

        if (opt.Check() == Status.SATISFIABLE)
        {
            var model = opt.Model;
            for (int i = 0; i < m; i++)
            {
                var val = model.Evaluate(sellVars[i], true);
                if (val.IsTrue)
                {
                    var lot = lossingLots[i];
                    candidates.Add(new
                    {
                        lotId = lot.LotId,
                        symbol = lot.Symbol,
                        purchaseDate = lot.PurchaseDate.ToString("yyyy-MM-dd"),
                        shares = lot.Shares,
                        purchasePrice = lot.PurchasePrice,
                        currentPrice = lot.CurrentPrice,
                        loss = Math.Round(losses[i], 2),
                        taxSavingsAt22Pct = Math.Round(losses[i] * 0.22, 2)
                    });
                    totalHarvest += losses[i];
                }
            }
        }

        var response = new
        {
            candidates,
            totalHarvestableLoss = Math.Round(totalHarvest, 2),
            estimatedTaxSavings = Math.Round(totalHarvest * 0.22, 2),
            washSaleWarnings,
            note = "These are candidates for tax-loss harvesting. Please review before executing any trades."
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── Tool 3: Compute Tax Savings (TensorPrimitives) ───────────────────────

    [Description("Computes estimated annual tax savings from optimized asset location using vectorized math")]
    public static string ComputeTaxSavings()
    {
        var holdings = LoadHoldings();
        var marketData = LoadMarketData();

        int n = holdings.Count;
        var values = new float[n];
        var divYields = new float[n];
        var expReturns = new float[n];
        var isTaxable = new float[n];

        for (int i = 0; i < n; i++)
        {
            var h = holdings[i];
            values[i] = (float)(h.Shares * h.CurrentPrice);
            if (marketData.TryGetValue(h.Symbol, out var info))
            {
                divYields[i] = (float)info.DividendYield;
                expReturns[i] = (float)info.ExpectedReturn;
            }
            isTaxable[i] = h.AccountType == "Taxable" ? 1.0f : 0.0f;
        }

        // Annual dividends per holding
        var annualDividends = new float[n];
        TensorPrimitives.Multiply(values, divYields, annualDividends);

        // Dividends currently taxed (in taxable accounts) at 22%
        var taxedDividends = new float[n];
        TensorPrimitives.Multiply(annualDividends, isTaxable, taxedDividends);

        float totalTaxableDividends = TensorPrimitives.Sum(taxedDividends);
        float dividendTaxDrag = totalTaxableDividends * 0.22f;

        // Growth tax drag estimate (15% LTCG on expected appreciation in taxable)
        var annualGrowth = new float[n];
        TensorPrimitives.Multiply(values, expReturns, annualGrowth);
        var taxableGrowth = new float[n];
        TensorPrimitives.Multiply(annualGrowth, isTaxable, taxableGrowth);
        float totalTaxableGrowth = TensorPrimitives.Sum(taxableGrowth);
        float growthTaxDrag = totalTaxableGrowth * 0.15f;

        // Bond interest in taxable (taxed as ordinary income at 22%)
        var bondInterest = new float[n];
        for (int i = 0; i < n; i++)
        {
            bool bond = marketData.TryGetValue(holdings[i].Symbol, out var info) && info.Sector == "Bonds";
            bondInterest[i] = bond ? annualDividends[i] * isTaxable[i] : 0f;
        }
        float totalBondInterest = TensorPrimitives.Sum(bondInterest);
        float bondTaxDrag = totalBondInterest * 0.22f;

        // Potential savings if moved to tax-advantaged
        float dividendSavings = dividendTaxDrag * 0.7f;
        float growthSavings = growthTaxDrag * 0.5f;
        float bondSavings = bondTaxDrag;

        var response = new
        {
            currentAnnualTaxDrag = new
            {
                dividendTax = Math.Round(dividendTaxDrag, 2),
                capitalGainsTax = Math.Round(growthTaxDrag, 2),
                bondInterestTax = Math.Round(bondTaxDrag, 2),
                total = Math.Round(dividendTaxDrag + growthTaxDrag + bondTaxDrag, 2)
            },
            potentialSavings = new
            {
                dividendSheltering = Math.Round(dividendSavings, 2),
                growthInRoth = Math.Round(growthSavings, 2),
                bondInterestSheltering = Math.Round(bondSavings, 2),
                total = Math.Round(dividendSavings + growthSavings + bondSavings, 2)
            }
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    // ── Tool 4: Render Tax Chart (Plotly.NET waterfall) ──────────────────────

    [Description("Renders a Plotly waterfall chart of tax optimization savings and saves it as an HTML file")]
    public static string RenderTaxChart()
    {
        var savingsJson = ComputeTaxSavings();
        var savingsData = JsonNode.Parse(savingsJson)!;

        var divShelter = savingsData["potentialSavings"]!["dividendSheltering"]!.GetValue<double>();
        var growthRoth = savingsData["potentialSavings"]!["growthInRoth"]!.GetValue<double>();
        var bondShelter = savingsData["potentialSavings"]!["bondInterestSheltering"]!.GetValue<double>();
        var totalDrag = savingsData["currentAnnualTaxDrag"]!["total"]!.GetValue<double>();
        var totalSavings = savingsData["potentialSavings"]!["total"]!.GetValue<double>();

        var xLabels = new[] { "Current Tax Drag", "Dividend Sheltering", "Growth in Roth", "Bond Sheltering", "Optimized Tax Drag" };
        var yValues = new[] { totalDrag, -divShelter, -growthRoth, -bondShelter, totalDrag - totalSavings };
        var measure = new[]
        {
            Plotly.NET.StyleParam.WaterfallMeasure.Absolute,
            Plotly.NET.StyleParam.WaterfallMeasure.Relative,
            Plotly.NET.StyleParam.WaterfallMeasure.Relative,
            Plotly.NET.StyleParam.WaterfallMeasure.Relative,
            Plotly.NET.StyleParam.WaterfallMeasure.Total
        };

        var chart = Chart.Waterfall<string, double, string>(
            x: xLabels,
            y: yValues,
            Measure: measure
        )
        .WithTraceInfo("Tax Optimization Savings Breakdown")
        .WithYAxisStyle<double, double, double>(Title: Plotly.NET.Title.init("Annual Tax ($)"));

        var filePath = Path.Combine(ChartsDir, "tax-savings.html");
        Plotly.NET.GenericChartExtensions.SaveHtml(chart, filePath);

        return JsonSerializer.Serialize(new
        {
            chartPath = filePath,
            message = "Waterfall chart saved showing tax optimization savings by category."
        });
    }
}
