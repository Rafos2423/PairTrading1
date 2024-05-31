namespace PairTrading
{
    public class Programm
    {
        public static async Task Main()
        {
            Extreme.License.Verify("51746-36039-12175-60593");

            var process = new ProcessPairs();
            process.StartProcess();

            Console.ReadKey();
        }
    }
}