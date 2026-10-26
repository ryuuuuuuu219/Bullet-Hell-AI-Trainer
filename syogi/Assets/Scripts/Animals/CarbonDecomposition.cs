using System;
using System.Collections.Generic;

public static class CarbonDecomposition
{
    // Decimal arithmetic avoids repeated binary subtraction producing tiny spurious particles.
    // Conversion uses decimal's precision; values below its smallest unit round to zero.
    public static IEnumerable<double> Split(double carbon)
    {
        if (double.IsNaN(carbon) || double.IsInfinity(carbon) || carbon <= 0 || carbon > (double)decimal.MaxValue)
            yield break;
        decimal remaining = (decimal)carbon;
        decimal unit = 100000m;
        while (remaining > 0 && unit > 0)
        {
            while (remaining >= unit * 3)
            {
                yield return (double)(unit * 3);
                remaining -= unit * 3;
            }
            while (remaining >= unit)
            {
                yield return (double)unit;
                remaining -= unit;
            }
            unit /= 10;
        }
    }
}
