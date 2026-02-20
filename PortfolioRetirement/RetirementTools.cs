using System.ComponentModel;
using System.Numerics.Tensors;
using System.Text.Json;
using System.Text.Json.Nodes;
using Plotly.NET;
using Plotly.NET.CSharp;
using Chart = Plotly.NET.CSharp.Chart;

namespace PortfolioRetirement;

/// <summary>
/// Retirement planning tools: Monte Carlo simulation, withdrawal strategy comparison,
/// Social Security optimization, and Plotly.NET chart rendering.
/// </summary>
internal static class RetirementTools
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

    [Description("Runs a Monte Carlo simulation of portfolio growth and returns percentile outcomes, probability of reaching the $2M goal, and mean final value")]
    public static string RunMonteCarlo(
        [Description("Expected annual return (e.g. 0.08 for 8%)")] double expectedReturn,
        [Description("Annual volatility / standard deviation (e.g. 0.15)")] double volatility,
        [Description("Number of years to simulate")] int years,
        [Description("Number of simulations to run (default 10000)")] int simCount = 10000)
    {
        var profile = LoadJson("investor_profile.json");
        double startingBalance =
            profile["accounts"]!["taxable"]!["balance"]!.GetValue<double>() +
            profile["accounts"]!["roth"]!["balance"]!.GetValue<double>() +
            profile["accounts"]!["traditional"]!["balance"]!.GetValue<double>();
        double annualContribution = profile["monthlyContribution"]!.GetValue<double>() * 12;

        double[] results = MonteCarloEngine.RunSimulation(
            startingBalance, annualContribution, expectedReturn, volatility, years, simCount);

        double goal = 2_000_000;
        int aboveGoal = 0;
        for (int i = 0; i < results.Length; i++)
            if (results[i] >= goal) aboveGoal++;

        var output = new
        {
            simulations = simCount,
            yearsSimulated = years,
            startingBalance = Math.Round(startingBalance, 2),
            annualContribution = Math.Round(annualContribution, 2),
            percentiles = new
            {
                p10 = Math.Round(results[(int)(simCount * 0.10)], 2),
                p25 = Math.Round(results[(int)(simCount * 0.25)], 2),
                p50 = Math.Round(results[(int)(simCount * 0.50)], 2),
                p75 = Math.Round(results[(int)(simCount * 0.75)], 2),
                p90 = Math.Round(results[(int)(simCount * 0.90)], 2)
            },
            probabilityOfReachingGoal = Math.Round((double)aboveGoal / simCount * 100, 2),
            meanFinalValue = Math.Round(results.Average(), 2)
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Compares three withdrawal strategies (4% Rule, Dynamic Percentage, Guardrails) over a 30-year retirement via Monte Carlo simulation")]
    public static string CompareWithdrawalStrategies(
        [Description("Starting retirement portfolio balance")] double retirementBalance,
        [Description("Annual expenses in retirement")] double annualExpenses,
        [Description("Annual inflation rate (e.g. 0.025 for 2.5%)")] double inflationRate)
    {
        const int retirementYears = 30;
        const int sims = 1000;
        double meanReturn = 0.07;
        double stdDev = 0.12;

        // Strategy 1: 4% Rule
        var fourPct = SimulateWithdrawals(retirementBalance, retirementYears, sims, meanReturn, stdDev,
            (balance, year, initialBalance) =>
            {
                double withdrawal = initialBalance * 0.04 * Math.Pow(1 + inflationRate, year);
                return Math.Min(withdrawal, balance);
            });

        // Strategy 2: Dynamic Percentage
        var dynamic = SimulateWithdrawals(retirementBalance, retirementYears, sims, meanReturn, stdDev,
            (balance, year, _) =>
            {
                double rate = balance > retirementBalance ? 0.05 : 0.04;
                return balance * rate;
            });

        // Strategy 3: Guardrails
        double baseWithdrawal = retirementBalance * 0.045;
        var guardrails = SimulateWithdrawals(retirementBalance, retirementYears, sims, meanReturn, stdDev,
            (balance, year, initialBalance) =>
            {
                double floor = initialBalance * 0.7;
                double ceiling = initialBalance * 1.5;
                double w = baseWithdrawal * Math.Pow(1 + inflationRate, year);
                if (balance < floor) w *= 0.90;
                else if (balance > ceiling) w *= 1.05;
                return Math.Min(w, balance);
            });

        var output = new
        {
            strategies = new[]
            {
                new { name = "4% Rule", successRate = fourPct.SuccessRate, medianAnnualIncome = fourPct.MedianIncome, medianRemainingBalance = fourPct.MedianRemaining },
                new { name = "Dynamic Percentage", successRate = dynamic.SuccessRate, medianAnnualIncome = dynamic.MedianIncome, medianRemainingBalance = dynamic.MedianRemaining },
                new { name = "Guardrails", successRate = guardrails.SuccessRate, medianAnnualIncome = guardrails.MedianIncome, medianRemainingBalance = guardrails.MedianRemaining }
            },
            retirementYears,
            startingBalance = Math.Round(retirementBalance, 2)
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Optimizes Social Security claiming ages for primary and spouse to maximize total lifetime benefits")]
    public static string OptimizeSocialSecurity()
    {
        var profile = LoadJson("investor_profile.json");
        var ssData = LoadJson("social_security.json");

        int primaryAge = profile["age"]!.GetValue<int>();
        int lifeExpectancy = profile["lifeExpectancy"]!.GetValue<int>();
        int spouseAge = profile["socialSecurity"]!["spouseAge"]!.GetValue<int>();
        double cola = ssData["colaAssumption"]!.GetValue<double>();

        var factors = ssData["reductionFactors"]!.AsObject();

        double primaryAt67 = profile["socialSecurity"]!["estimatedMonthlyAt67"]!.GetValue<double>();
        double spouseAt67 = profile["socialSecurity"]!["spouseEstimatedMonthlyAt67"]!.GetValue<double>();

        var results = new List<(int primaryClaim, int spouseClaim, double totalBenefits)>();

        int[] claimingAges = { 62, 63, 64, 65, 66, 67, 68, 69, 70 };

        foreach (int pClaim in claimingAges)
        {
            foreach (int sClaim in claimingAges)
            {
                double pFactor = factors[pClaim.ToString()]!.GetValue<double>();
                double sFactor = factors[sClaim.ToString()]!.GetValue<double>();

                double pMonthly = primaryAt67 * pFactor;
                double sMonthly = spouseAt67 * sFactor;

                // Build annual benefit streams with COLA
                int pYearsReceiving = lifeExpectancy - pClaim;
                int sYearsReceiving = lifeExpectancy - sClaim + (primaryAge - spouseAge);

                if (pYearsReceiving <= 0 || sYearsReceiving <= 0) continue;

                double[] pStream = new double[pYearsReceiving];
                for (int y = 0; y < pYearsReceiving; y++)
                    pStream[y] = pMonthly * 12 * Math.Pow(1 + cola, y);

                double[] sStream = new double[sYearsReceiving];
                for (int y = 0; y < sYearsReceiving; y++)
                    sStream[y] = sMonthly * 12 * Math.Pow(1 + cola, y);

                double totalP = TensorPrimitives.Sum<double>(pStream);
                double totalS = TensorPrimitives.Sum<double>(sStream);

                results.Add((pClaim, sClaim, totalP + totalS));
            }
        }

        var top3 = results
            .OrderByDescending(r => r.totalBenefits)
            .Take(3)
            .Select(r => new
            {
                primaryClaimingAge = r.primaryClaim,
                spouseClaimingAge = r.spouseClaim,
                totalLifetimeBenefits = Math.Round(r.totalBenefits, 2)
            })
            .ToArray();

        var output = new
        {
            analysis = "Top 3 Social Security claiming strategies ranked by total lifetime benefits",
            lifeExpectancy,
            colaAssumption = cola,
            topStrategies = top3
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Renders a probability cone chart showing Monte Carlo portfolio projection percentiles and saves it as an HTML file")]
    public static string RenderProbabilityCone(
        [Description("Expected annual return")] double expectedReturn,
        [Description("Annual volatility")] double volatility,
        [Description("Number of years to simulate")] int years)
    {
        var profile = LoadJson("investor_profile.json");
        double startingBalance =
            profile["accounts"]!["taxable"]!["balance"]!.GetValue<double>() +
            profile["accounts"]!["roth"]!["balance"]!.GetValue<double>() +
            profile["accounts"]!["traditional"]!["balance"]!.GetValue<double>();
        double annualContribution = profile["monthlyContribution"]!.GetValue<double>() * 12;

        var percentiles = MonteCarloEngine.RunWithYearlyPercentiles(
            startingBalance, annualContribution, expectedReturn, volatility, years);

        double[] xYears = Enumerable.Range(0, years + 1).Select(y => (double)y).ToArray();
        double goal = 2_000_000;
        double[] goalLine = Enumerable.Repeat(goal, years + 1).ToArray();

        var t1 = Chart.Line<double, double, string>(x: xYears, y: percentiles["p90"], Name: "90th Percentile",
            LineColor: Plotly.NET.Color.fromARGB(80, 0, 128, 0));
        var t2 = Chart.Line<double, double, string>(x: xYears, y: percentiles["p75"], Name: "75th Percentile",
            LineColor: Plotly.NET.Color.fromARGB(120, 0, 128, 0));
        var t3 = Chart.Line<double, double, string>(x: xYears, y: percentiles["p50"], Name: "Median (50th)",
            LineColor: Plotly.NET.Color.fromARGB(255, 0, 100, 0));
        var t4 = Chart.Line<double, double, string>(x: xYears, y: percentiles["p25"], Name: "25th Percentile",
            LineColor: Plotly.NET.Color.fromARGB(120, 200, 100, 0));
        var t5 = Chart.Line<double, double, string>(x: xYears, y: percentiles["p10"], Name: "10th Percentile",
            LineColor: Plotly.NET.Color.fromARGB(80, 200, 0, 0));
        var t6 = Chart.Line<double, double, string>(x: xYears, y: goalLine, Name: "$2M Goal",
            LineDash: Plotly.NET.StyleParam.DrawingStyle.Dash,
            LineColor: Plotly.NET.Color.fromARGB(200, 255, 0, 0));

        var chart = Chart.Combine(new[] { t1, t2, t3, t4, t5, t6 })
            .WithTitle("Portfolio Growth Probability Cone (Monte Carlo)")
            .WithXAxisStyle<double, double, double>(Title: Plotly.NET.Title.init("Years"))
            .WithYAxisStyle<double, double, double>(Title: Plotly.NET.Title.init("Portfolio Value ($)"));

        Directory.CreateDirectory(ChartsDir);
        string filePath = Path.Combine(ChartsDir, "probability-cone.html");
        Plotly.NET.CSharp.GenericChartExtensions.SaveHtml(chart, filePath);

        var p50Final = percentiles["p50"].Last();
        var p10Final = percentiles["p10"].Last();
        var p90Final = percentiles["p90"].Last();
        var interpretation = $"This probability cone shows 10,000 simulated futures for your portfolio. " +
            $"In the most likely scenario (middle line), your portfolio reaches ${p50Final/1_000_000:F1}M. " +
            $"In a bad market (bottom), it could be ${p10Final/1_000_000:F1}M. " +
            $"In a strong market (top), it could reach ${p90Final/1_000_000:F1}M. " +
            $"The dashed line shows your ${2_000_000/1_000_000:F0}M goal.";

        return JsonSerializer.Serialize(new
        {
            chart = "probability-cone",
            savedTo = filePath,
            message = "Probability cone chart saved. Open the HTML file in a browser to view.",
            interpretation
        });
    }

    [Description("Renders a grouped bar chart comparing withdrawal strategies on success rate, median income, and median remaining balance")]
    public static string RenderStrategyComparison(
        [Description("JSON string containing withdrawal strategy comparison results")] string strategyResultsJson)
    {
        using var doc = JsonDocument.Parse(strategyResultsJson);
        var strategies = doc.RootElement.GetProperty("strategies");

        var names = new List<string>();
        var successRates = new List<double>();
        var medianIncomes = new List<double>();
        var medianRemaining = new List<double>();

        foreach (var s in strategies.EnumerateArray())
        {
            names.Add(s.GetProperty("name").GetString()!);
            successRates.Add(s.GetProperty("successRate").GetDouble());
            medianIncomes.Add(s.GetProperty("medianAnnualIncome").GetDouble());
            medianRemaining.Add(s.GetProperty("medianRemainingBalance").GetDouble());
        }

        var c1 = Chart.Column<double, string, string>(
            values: successRates, Keys: names, Name: "Success Rate (%)");
        var c2 = Chart.Column<double, string, string>(
            values: medianIncomes.Select(v => v / 1000), Keys: names, Name: "Median Income ($K)");
        var c3 = Chart.Column<double, string, string>(
            values: medianRemaining.Select(v => v / 1000), Keys: names, Name: "Median Remaining ($K)");

        var chart = Chart.Combine(new[] { c1, c2, c3 })
            .WithTitle("Withdrawal Strategy Comparison");

        Directory.CreateDirectory(ChartsDir);
        string filePath = Path.Combine(ChartsDir, "strategy-comparison.html");
        Plotly.NET.CSharp.GenericChartExtensions.SaveHtml(chart, filePath);

        var interpretation = "This chart compares three retirement withdrawal strategies side by side. " +
            "Look at the 'Success Rate' bars — that's the chance your money lasts through retirement. " +
            "Higher income means more spending each year, but check if the success rate is still comfortable for you.";

        return JsonSerializer.Serialize(new
        {
            chart = "strategy-comparison",
            savedTo = filePath,
            message = "Strategy comparison chart saved. Open the HTML file in a browser to view.",
            interpretation
        });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static (double SuccessRate, double MedianIncome, double MedianRemaining)
        SimulateWithdrawals(double startBalance, int years, int sims,
            double meanReturn, double stdDev,
            Func<double, int, double, double> withdrawalFunc)
    {
        int survived = 0;
        double[] finalBalances = new double[sims];
        double[] totalIncomes = new double[sims];

        Span<double> returns = new double[sims];

        for (int sim = 0; sim < sims; sim++)
        {
            double balance = startBalance;
            double totalIncome = 0;
            bool depleted = false;

            for (int year = 0; year < years; year++)
            {
                if (balance <= 0) { depleted = true; break; }

                double withdrawal = withdrawalFunc(balance, year, startBalance);
                balance -= withdrawal;
                totalIncome += withdrawal;

                // Apply market return
                MonteCarloEngine.FillNormalRandom(returns.Slice(sim, 1), meanReturn, stdDev);
                balance *= (1 + returns[sim]);
                if (balance < 0) balance = 0;
            }

            if (!depleted && balance > 0) survived++;
            finalBalances[sim] = balance;
            totalIncomes[sim] = totalIncome;
        }

        Array.Sort(finalBalances);
        Array.Sort(totalIncomes);

        double medianIncome = totalIncomes[sims / 2] / years;
        double medianRemaining = finalBalances[sims / 2];
        double successRate = Math.Round((double)survived / sims * 100, 1);

        return (successRate, Math.Round(medianIncome, 2), Math.Round(medianRemaining, 2));
    }

    private static JsonNode LoadJson(string fileName)
    {
        string path = Path.Combine(DataDir, fileName);
        string json = File.ReadAllText(path);
        return JsonNode.Parse(json)!;
    }
}
