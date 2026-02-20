using System.Numerics.Tensors;

namespace PortfolioRetirement;

internal static class MonteCarloEngine
{
    // Run 10K simulations returning final balances
    public static double[] RunSimulation(
        double startingBalance, double annualContribution,
        double meanReturn, double stdDev, int years, int simCount = 10000)
    {
        Span<double> balances = new double[simCount];
        balances.Fill(startingBalance);

        Span<double> returns = new double[simCount];
        Span<double> growthFactors = new double[simCount];
        Span<double> contributions = new double[simCount];
        contributions.Fill(annualContribution);

        for (int year = 0; year < years; year++)
        {
            FillNormalRandom(returns, meanReturn, stdDev);
            TensorPrimitives.Add(returns, 1.0, growthFactors);
            TensorPrimitives.Multiply(balances, growthFactors, balances);
            TensorPrimitives.Add(balances, contributions, balances);
        }

        double[] result = balances.ToArray();
        Array.Sort(result);
        return result;
    }

    // Run simulation tracking year-by-year percentiles for probability cone
    public static Dictionary<string, double[]> RunWithYearlyPercentiles(
        double startingBalance, double annualContribution,
        double meanReturn, double stdDev, int years, int simCount = 10000)
    {
        var p10 = new double[years + 1];
        var p25 = new double[years + 1];
        var p50 = new double[years + 1];
        var p75 = new double[years + 1];
        var p90 = new double[years + 1];

        p10[0] = p25[0] = p50[0] = p75[0] = p90[0] = startingBalance;

        Span<double> balances = new double[simCount];
        balances.Fill(startingBalance);
        Span<double> returns = new double[simCount];
        Span<double> growthFactors = new double[simCount];
        Span<double> contributions = new double[simCount];
        contributions.Fill(annualContribution);

        for (int year = 0; year < years; year++)
        {
            FillNormalRandom(returns, meanReturn, stdDev);
            TensorPrimitives.Add(returns, 1.0, growthFactors);
            TensorPrimitives.Multiply(balances, growthFactors, balances);
            TensorPrimitives.Add(balances, contributions, balances);

            double[] sorted = balances.ToArray();
            Array.Sort(sorted);
            p10[year + 1] = sorted[(int)(simCount * 0.10)];
            p25[year + 1] = sorted[(int)(simCount * 0.25)];
            p50[year + 1] = sorted[(int)(simCount * 0.50)];
            p75[year + 1] = sorted[(int)(simCount * 0.75)];
            p90[year + 1] = sorted[(int)(simCount * 0.90)];
        }

        return new Dictionary<string, double[]>
        {
            ["p10"] = p10, ["p25"] = p25, ["p50"] = p50, ["p75"] = p75, ["p90"] = p90
        };
    }

    // Box-Muller normal distribution (no external library)
    public static void FillNormalRandom(Span<double> span, double mean, double stdDev)
    {
        var rng = Random.Shared;
        for (int i = 0; i < span.Length - 1; i += 2)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double z1 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            span[i] = mean + stdDev * z0;
            span[i + 1] = mean + stdDev * z1;
        }
        if (span.Length % 2 != 0)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            span[^1] = mean + stdDev * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
