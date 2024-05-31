using Extreme.Mathematics;
using Extreme.Statistics.TimeSeriesAnalysis;

namespace PairTrading
{
    public class Indicators
    {
        public static double GetZscoreValue(double[] prices, int length)
        {
            var sma = SMA(prices, length);
            return (prices[0] - sma) / Math.Sqrt(prices.Select(x => Square(x - sma)).Sum() / length);
        }

        public static bool MakeTest(double[] first, double[] second)
        {
            var ratio = Utils.GetRatio(first, second);

            var test2 = new AugmentedDickeyFullerTest(Vector.Create(ratio), 2);
            var test3 = new AugmentedDickeyFullerTest(Vector.Create(ratio), 3);

            return test2.Statistic < -4 && test2.PValue < 0.05 &&
                   test3.Statistic < -4 && test3.PValue < 0.05;
        }

        private static double Square(double num) => num * num;

        private static double SMA(double[] src, int length) => src.Take(length).Sum() / length;
    }
}
