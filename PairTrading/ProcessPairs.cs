using PairTrading.API;
using PairTrading.Models;

namespace PairTrading
{
    public class ProcessPairs
    {
        private List<TradePair> Pairs = new();

        private Timer Testing;
        private Timer Filtering;
        private Timer Opening;
        private Timer Closing;

        private object locker = new();

        public ProcessPairs()
        {
            Testing = new Timer(_ => Callback(TestAsync), null, Timeout.Infinite, Timeout.Infinite);
            Filtering = new Timer(_ => Callback(FilterAsync), null, Timeout.Infinite, Timeout.Infinite);
            Opening = new Timer(_ => Callback(OpenAsync), null, Timeout.Infinite, Timeout.Infinite);
            Closing = new Timer(_ => Callback(CloseAsync), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void StartProcess()
        {
            Callback(SyncOpenedAsync);
        }

        private async Task TestAsync()
        {
            var pairs = await BybitRequests.GetPairsAsync();

            var prices = new List<double[]>();
            var prices4H = new List<double[]>();

            foreach (var pair in new List<string>(pairs))
            {
                var hourPrices = await BybitRequests.GetPricesAsync(pair, 60, 720);
                var hour4Prices = await BybitRequests.GetPricesAsync(pair, 240, 180);

                if (hourPrices.Length >= 720)
                {
                    prices.Add(hourPrices);
                    prices4H.Add(hour4Prices);
                }
                else pairs.Remove(pair);
            };

            for (var i = 0; i < pairs.Count; i++)
            {
                for (var j = 0; j < pairs.Count; j++)
                {
                    if (j == i) continue;

                    var names = (pairs[i], pairs[j]);
                    var equalPair = Pairs.FirstOrDefault(x => x.OneNameEqual(names));

                    var testSuccess = Indicators.MakeTest(prices[i], prices[j]) && Indicators.MakeTest(prices4H[i], prices4H[j]);

                    if (equalPair == null && testSuccess)
                        Pairs.Add(new TradePair(names));
                    else if (!testSuccess && equalPair != null && equalPair.BothNameEqual(names) && equalPair.GetState() == State.Opened)
                    {
                        equalPair.GetCloseInfo(CloseReason.TestFailed);
                        await Utils.CallRepeatAsync(BybitRequests.PlaceOrderAsync, equalPair.GetNames(), equalPair.GetReversedSides(), equalPair.GetVolumes(), true);
                        Pairs.Remove(equalPair);
                    }
                }
            }
        }

        private async Task FilterAsync()
        {
            var pairs = Pairs.Where(x => x.GetState() == State.Tested);
            var clonedPairs = new List<TradePair>(pairs);

            foreach (var pair in clonedPairs)
            {
                var prices = await Utils.CallRepeatAsync(BybitRequests.GetPricesAsync, pair.GetNames(), 60, 20);
                var ratio = Utils.GetRatio(prices.Item1, prices.Item2);
                var zscore = Indicators.GetZscoreValue(ratio, 20);

                if (zscore < -2) pair.SetSide((Side.Buy, Side.Sell));
                else if (zscore > 2) pair.SetSide((Side.Sell, Side.Buy));
            }
        }

        private async Task OpenAsync()
        {
            var pairs = Pairs.Where(x => x.GetState() == State.Filtered);
            var clonedPairs = new List<TradePair>(pairs);
            var openedPairsCount = Pairs.Count(x => x.GetState() == State.Opened);

            foreach (var pair in clonedPairs)
            {
                var prices = await Utils.CallRepeatAsync(BybitRequests.GetPricesAsync, pair.GetNames(), 60, 20);
                var ratio = Utils.GetRatio(prices.Item1, prices.Item2);
                var zscore = Indicators.GetZscoreValue(ratio, 20);

                if (Math.Abs(zscore) >= 2)
                    continue;
                if (openedPairsCount >= 10)
                {
                    Pairs.Remove(pair);
                    continue;
                }

                var balance = await BybitRequests.GetAccountBalanceAsync();
                var priceSteps = await Utils.CallRepeatAsync(BybitRequests.GetPriceStepAsync, pair.GetNames());
                pair.SetVolume(balance, prices.Item1[0], prices.Item2[0], priceSteps);
                if (pair.IsVolumeZero())
                {
                    Pairs.Remove(pair);
                    continue;
                }

                pair.Open();
                await Utils.CallRepeatAsync(BybitRequests.PlaceOrderAsync, pair.GetNames(), pair.GetSides(), pair.GetVolumes());
                openedPairsCount++;
            }
        }

        private async Task CloseAsync()
        {
            var pairs = Pairs.Where(x => x.GetState() == State.Opened).ToList();
            var clonedPairs = new List<TradePair>(pairs);

            foreach (var pair in clonedPairs)
            {
                var reason = CloseReason.None;

                if (pair.IsTimeout())
                    reason = CloseReason.Timeout;
                else
                {
                    var prices = await Utils.CallRepeatAsync(BybitRequests.GetPricesAsync, pair.GetNames(), 60, 20);
                    var ratio = Utils.GetRatio(prices.Item1, prices.Item2);
                    var zscore = Indicators.GetZscoreValue(ratio, 20);

                    if (pair.GetSides().Item1 == Side.Buy && zscore >= 0 ||
                        pair.GetSides().Item1 == Side.Sell && zscore <= 0)
                        reason = CloseReason.Zscore;
                    else
                        continue;
                }

                pair.GetCloseInfo(reason);
                await Utils.CallRepeatAsync(BybitRequests.PlaceOrderAsync, pair.GetNames(), pair.GetReversedSides(), pair.GetVolumes(), true);
                Pairs.Remove(pair);
            }
        }

        public async Task SyncOpenedAsync()
        {
            var pairNames = await BybitRequests.GetOpenedPairsAsync();
            var cursor = string.Empty;

            while (pairNames.Any())
            {
                var response = await BybitRequests.GetTradesHistoryAsync(cursor);
                var trades = response["list"];
                cursor = response["nextPageCursor"].ToString();

                for (var i = 0; i < trades.Count(); i += 2)
                {
                    var history = (trades[i + 1], trades[i]);

                    if (pairNames.Contains(history.Item1["symbol"].ToString()) &&
                        pairNames.Contains(history.Item2["symbol"].ToString()))
                    {
                        if (Utils.ToLong(history.Item1["createdTime"]) - Utils.ToLong(history.Item2["createdTime"]) < 3000)
                        {
                            var names = Utils.CallRepeat(history, "symbol", Utils.ToStr);
                            var sides = Utils.CallRepeat(history, "side", Utils.ToSide);
                            var volumes = Utils.CallRepeat(history, "qty", Utils.ToDouble);

                            if (sides.Item1 != sides.Item2)
                            {
                                var openTime = DateTimeOffset.FromUnixTimeMilliseconds(Utils.ToLong(history.Item1["createdTime"])).DateTime.ToLocalTime();
                                await Console.Out.WriteLineAsync($"{DateTime.Now:HH:mm:ss} - Sync \t{names.Item1} {names.Item2}");

                                Pairs.Add(new TradePair(names, sides, volumes, openTime));

                                pairNames.Remove(names.Item1);
                                pairNames.Remove(names.Item2);
                            }
                        }
                    }
                }
            }
        }

        private void Callback(Func<Task> process)
        {
            try
            {
                if (process == SyncOpenedAsync)
                {
                    SyncOpenedAsync().Wait();
                    StartTimer(Testing, TimeSpan.FromHours(3));
                }
                else if (process == TestAsync)
                {
                    StopTimer(Opening);
                    StopTimer(Closing);
                    StopTimer(Filtering);
                    lock (locker) TestAsync().Wait();
                    StartTimer(Filtering, TimeSpan.FromMinutes(3));
                }
                else if (process == FilterAsync)
                {
                    StopTimer(Opening);
                    StopTimer(Closing);
                    lock (locker) FilterAsync().Wait();
                    StartTimer(Opening, TimeSpan.FromSeconds(30));
                    StartTimer(Closing, TimeSpan.FromSeconds(30));
                }
                else
                {
                    lock (locker) process().Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} - {process.Method.Name[..^5]} - Exception\t{ex.Message.Replace("\n", " ")}");
            }
        }

        private void StopTimer(Timer timer) => timer.Change(Timeout.Infinite, Timeout.Infinite);
        private void StartTimer(Timer timer, TimeSpan period) => timer.Change(TimeSpan.Zero, period);
    }
}
