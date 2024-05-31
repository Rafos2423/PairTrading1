namespace PairTrading.Models
{
    public enum State { Tested, Filtered, Opened }
    public enum Side { Buy, Sell }
    public enum CloseReason { None, Zscore, Timeout, TestFailed }

    public class TradePair
    {
        private (string, string) Names { get; set; }
        private (double, double) Volumes { get; set; }
        private (Side, Side) Sides { get; set; }
        private State State { get; set; }
        private DateTime OpenAt { get; set; }

        public TradePair((string, string) names, (Side, Side) sides, (double, double) volumes, DateTime openAt)
        {
            Names = names;
            Sides = sides;
            Volumes = volumes;
            State = State.Opened;
            OpenAt = openAt;
        }

        public TradePair((string, string) names)
        {
            Names = names;
            State = State.Tested;
            GetInfo();
        }

        public void SetSide((Side, Side) sides)
        {
            Sides = sides;
            State = State.Filtered;
            GetInfo();
        }

        public void Open()
        {
            OpenAt = DateTime.Now;
            State = State.Opened;
            GetInfo();
        }

        public void SetVolume(double balance, double priceFirst, double priceSecond, (double, double) priceStep)
        {
            Volumes = (CalculateVolume(balance, priceFirst, priceStep.Item1),
                       CalculateVolume(balance, priceSecond, priceStep.Item2));
        }

        private static double CalculateVolume(double balance, double price, double priceStep)
        {
            var coins = balance * 0.3 / price;
            var result = coins - coins % priceStep;
            return Math.Round(result, 6);
        }


        public bool IsTimeout() => OpenAt.AddDays(3) < DateTime.Now;
        public bool IsVolumeZero() => Volumes.Item1 == 0 || Volumes.Item2 == 0;
        public bool OneNameEqual((string, string) names) => Names.Item1 == names.Item1 || Names.Item2 == names.Item2 || Names.Item1 == names.Item2 || Names.Item2 == names.Item1;
        public bool BothNameEqual((string, string) names) => Names.Item1 == names.Item1 && Names.Item2 == names.Item2;


        public State GetState() => State;
        public DateTime GetOpenedTime() => OpenAt;
        public (string, string) GetNames() => (Names.Item1, Names.Item2);
        public (Side, Side) GetSides() => (Sides.Item1, Sides.Item2);
        public (Side, Side) GetReversedSides() => (Sides.Item2, Sides.Item1);
        public (double, double) GetVolumes() => (Volumes.Item1, Volumes.Item2);
        public void GetInfo() => Console.WriteLine($"{DateTime.Now:HH:mm:ss} - {State}\t{Names.Item1} {Names.Item2}");
        public void GetCloseInfo(CloseReason reason) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Closed\t{Names.Item1} {Names.Item2} by {reason}");
    }
}
