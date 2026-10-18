namespace FramePilot.Core.Metrics;

/// <summary>
/// Statistical helpers used by the frame time analysis.
/// </summary>
/// <remarks>
/// Percentiles are computed with linear interpolation between the two closest
/// order statistics (the "inclusive"/R-7 definition used by Excel's
/// <c>PERCENTILE.INC</c> and by NumPy's default). The definition is
/// documented in the user documentation so that results are reproducible.
/// </remarks>
public static class PercentileCalculator
{
    /// <summary>Returns the p-th percentile (0-100) of the given values.</summary>
    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (percentile < 0 || percentile > 100 || double.IsNaN(percentile))
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be between 0 and 100.");
        }

        var sorted = values.OrderBy(v => v).ToArray();

        if (sorted.Length == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }

        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        var rank = percentile / 100.0 * (sorted.Length - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
        {
            return sorted[lowerIndex];
        }

        var fraction = rank - lowerIndex;
        return sorted[lowerIndex] + (sorted[upperIndex] - sorted[lowerIndex]) * fraction;
    }

    /// <summary>Median (50th percentile).</summary>
    public static double Median(IEnumerable<double> values) => Percentile(values, 50);

    /// <summary>Arithmetic mean.</summary>
    public static double Mean(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var list = values as IReadOnlyList<double> ?? values.ToArray();

        if (list.Count == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }

        double sum = 0;
        foreach (var value in list)
        {
            sum += value;
        }

        return sum / list.Count;
    }

    /// <summary>Sample standard deviation (Bessel corrected).</summary>
    public static double? StandardDeviation(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var list = values as IReadOnlyList<double> ?? values.ToArray();

        if (list.Count < 2)
        {
            return null;
        }

        var mean = Mean(list);
        double sumOfSquares = 0;
        foreach (var value in list)
        {
            var delta = value - mean;
            sumOfSquares += delta * delta;
        }

        return Math.Sqrt(sumOfSquares / (list.Count - 1));
    }

    /// <summary>
    /// Median absolute deviation - a robust measure of spread that is not
    /// influenced by single outliers.
    /// </summary>
    public static double? MedianAbsoluteDeviation(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var list = values as IReadOnlyList<double> ?? values.ToArray();

        if (list.Count == 0)
        {
            return null;
        }

        var median = Median(list);
        var deviations = list.Select(v => Math.Abs(v - median)).ToArray();

        return Median(deviations);
    }

    /// <summary>
    /// Inter quartile range, a spread measure that is not distorted by outliers.
    /// </summary>
    public static double? InterQuartileRange(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var list = values as IReadOnlyList<double> ?? values.ToArray();

        if (list.Count == 0)
        {
            return null;
        }

        return Percentile(list, 75) - Percentile(list, 25);
    }
}
