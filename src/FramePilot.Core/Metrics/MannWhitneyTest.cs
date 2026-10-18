namespace FramePilot.Core.Metrics;

/// <summary>
/// Two sided Mann-Whitney U test (Wilcoxon rank sum test) with tie
/// correction and a normal approximation.
/// </summary>
/// <remarks>
/// The test is distribution free: it only assumes that the two samples come
/// from independent populations. It is used to decide whether the difference
/// between two benchmark configurations is larger than the run to run noise.
/// It never proves that one configuration *causes* a higher frame rate.
/// </remarks>
public static class MannWhitneyTest
{
    private const int MinimumSampleSize = 3;

    /// <summary>
    /// Returns the two sided p value, or <c>null</c> when the samples are too
    /// small for a meaningful approximation.
    /// </summary>
    public static double? CalculateTwoSidedPValue(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var n1 = first.Count;
        var n2 = second.Count;

        if (n1 < MinimumSampleSize || n2 < MinimumSampleSize)
        {
            return null;
        }

        var total = n1 + n2;
        var values = new List<(double Value, bool FromFirst)>(total);

        foreach (var value in first)
        {
            values.Add((value, true));
        }

        foreach (var value in second)
        {
            values.Add((value, false));
        }

        values.Sort(static (a, b) => a.Value.CompareTo(b.Value));

        var ranks = new double[total];
        var tieCorrection = 0.0;
        var index = 0;

        while (index < total)
        {
            var end = index;
            while (end + 1 < total && values[end + 1].Value.Equals(values[index].Value))
            {
                end++;
            }

            var tieSize = end - index + 1;
            var averageRank = ((index + 1) + (end + 1)) / 2.0;

            for (var i = index; i <= end; i++)
            {
                ranks[i] = averageRank;
            }

            tieCorrection += tieSize * tieSize * tieSize - tieSize;
            index = end + 1;
        }

        double rankSumFirst = 0;
        for (var i = 0; i < total; i++)
        {
            if (values[i].FromFirst)
            {
                rankSumFirst += ranks[i];
            }
        }

        var u1 = rankSumFirst - (n1 * (n1 + 1) / 2.0);
        var u2 = (double)n1 * n2 - u1;
        var u = Math.Min(u1, u2);

        var mean = n1 * n2 / 2.0;
        var variance = n1 * n2 / 12.0 * ((total + 1) - tieCorrection / (total * (double)(total - 1)));

        if (variance <= 0 || double.IsNaN(variance))
        {
            // All observations are identical: the two samples cannot be
            // distinguished at all, which is a p value of 1.
            return Math.Abs(u - mean) < 1e-9 ? 1.0 : null;
        }

        var z = (u - mean + 0.5) / Math.Sqrt(variance);

        // The two sided p value uses the absolute value of z.
        var p = 2.0 * (1.0 - NormalCumulative(Math.Abs(z)));

        return Math.Clamp(p, 0.0, 1.0);
    }

    /// <summary>Standard normal cumulative distribution function.</summary>
    public static double NormalCumulative(double z)
    {
        return 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));
    }

    /// <summary>
    /// Error function approximation (Abramowitz and Stegun 7.1.26), maximum
    /// error 1.5e-7.
    /// </summary>
    public static double Erf(double x)
    {
        var sign = x < 0 ? -1.0 : 1.0;
        var value = Math.Abs(x);

        var t = 1.0 / (1.0 + 0.3275911 * value);
        var y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-value * value);

        return sign * y;
    }
}
