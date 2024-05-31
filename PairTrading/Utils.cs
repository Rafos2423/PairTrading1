using Newtonsoft.Json.Linq;
using PairTrading.Models;

namespace PairTrading
{
    public static class Utils
    {
        public static string ToStr(double num) => num.ToString().Replace(',', '.');
        public static string ToStr(JToken str) => str.ToString();

        public static double ToDouble(string num)
        {
            if (string.IsNullOrEmpty(num)) throw new Exception($"Cant parse empty string to double number");

            return double.TryParse(num.Replace('.', ','), out var result) ? result :
                throw new Exception($"Cant parse {num} to number");
        }

        public static double ToDouble(JToken num)
        {
            if (num == null) throw new Exception($"Cant parse empty json to number");
            return ToDouble(num.ToString());
        }

        public static long ToLong(JToken num)
        {
            var numString = num.ToString();
            if (string.IsNullOrEmpty(numString)) throw new Exception($"Cant parse empty string to long number");

            return long.TryParse(numString, out var result) ? result :
                throw new Exception($"Cant parse {num} to long number");
        }

        public static Side ToSide(JToken side)
        {
            if (side == null) throw new Exception($"Cant parse empty json to side");

            return Enum.TryParse(side.ToString(), out Side result) ? result :
                throw new Exception($"Cant parse {side} to side");
        }

        public static (T, T) CallRepeat<T>((JToken, JToken) item, string take, Func<JToken, T> func) => (
            func(item.Item1[take]),
            func(item.Item2[take]));

        public static async Task<(T, T)> CallRepeatAsync<K, T>(Func<K, Task<T>> func, (K, K) parameters) => (
            await func(parameters.Item1), 
            await func(parameters.Item2));

        public static async Task<(T, T)> CallRepeatAsync<K, T, J>(Func<K, J, J, Task<T>> func, (K, K) parameters, J param1, J param2) => (
            await func(parameters.Item1, param1, param2),
            await func(parameters.Item2, param1, param2));

        public static async Task CallRepeatAsync(Func<string, string, string, bool, Task> func, (string, string) names, (Side, Side) sides, (double, double) volumes, bool reduce=false)
        {
            await func(names.Item1, sides.Item1.ToString(), ToStr(volumes.Item1), reduce);
            await func(names.Item2, sides.Item2.ToString(), ToStr(volumes.Item2), reduce);
        }

        public static double[] GetRatio(double[] first, double[] second) => first.Zip(second, (x, y) => x / y).ToArray();
    }
}
