using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PairTrading.API
{
    public static class BybitRequests
    {
        private const string apiKey = "l7ykUJXwgoYW5aLwTz";
        private const string apiSecret = "wVEVFqBOXvnuPj088mCWnwXHm6XQAMzy4W9F";

        public static async Task PlaceOrderAsync(string name, string side, string volume, bool reduce)
        {
            var parameters = new Dictionary<string, string>
            {
                { "category", "linear" },
                { "symbol", name },
                { "side", side },
                { "orderType", "Market" },
                { "qty", volume },
            };

            if (reduce) parameters.Add("reduceOnly", "true");

            await MakeRequestAsync(HttpMethod.Post, "/v5/order/create", parameters, true);
        }

        public static async Task<List<string>> GetPairsAsync()
        {
            var parameters = new Dictionary<string, string>
            {
                { "category", "linear" },
            };

            var response = await MakeRequestAsync(HttpMethod.Get, "/v5/market/tickers", parameters);
            var list = ResultList(response);
            var result = list.OrderBy(x => x["volume24h"]).Select(x => x["symbol"].ToString()).Where(x => x[^4..] == "USDT").ToList();
            return result;
        }

        public static async Task<JToken> GetTradesHistoryAsync(string cursor)
        {
            var parameters = new Dictionary<string, string>
            {
                { "category", "linear" },
                { "settleCoin", "USDT" },
                { "limit", "50" },
            };

            if (!string.IsNullOrEmpty(cursor)) parameters.Add("cursor", cursor);

            var response = await MakeRequestAsync(HttpMethod.Get, "/v5/order/history", parameters, true);
            return response["result"];
        }

        public static async Task<List<string>> GetOpenedPairsAsync()
        {
            var parameters = new Dictionary<string, string>
            {
                { "category", "linear" },
                { "settleCoin", "USDT" },
                { "limit", "200" },
            };

            var response = await MakeRequestAsync(HttpMethod.Get, "/v5/position/list", parameters, true);
            var list = ResultList(response);
            var result = list.Select(x => x["symbol"].ToString()).ToList();
            return result;
        }

        public static async Task<double[]> GetPricesAsync(string coinName, int interval, int count)
        {
            var parameters = new Dictionary<string, string>
            {
                { "category", "linear" },
                { "symbol", coinName },
                { "interval", interval.ToString() },
                { "limit", count.ToString() },
            };

            var response = await MakeRequestAsync(HttpMethod.Get, "/v5/market/mark-price-kline", parameters, true);
            var list = ResultList(response);
            var result = list.Select(x => Utils.ToDouble(x[4])).ToArray();
            return result;
        }

        public static async Task<double> GetPriceStepAsync(string pairName)
        {
            var parameters = new Dictionary<string, string>
            {
                { "category", "linear" },
                { "symbol",  pairName},
                { "baseCoin", "USDT"}
            };

            var response = await MakeRequestAsync(HttpMethod.Get, "/v5/market/instruments-info", parameters);
            var list = ResultList(response);
            var result = list.FirstOrDefault(x => x["symbol"]?.ToString() == pairName)["lotSizeFilter"]["qtyStep"].ToString();
            return Utils.ToDouble(result);
        }

        public static async Task<double> GetAccountBalanceAsync()
        {
            var parameters = new Dictionary<string, string>
            {
                { "accountType", "CONTRACT" },
                { "coin", "USDT" },
            };

            var response = await MakeRequestAsync(HttpMethod.Get, "/v5/account/wallet-balance", parameters, true);
            var list = ResultList(response);
            var result = list[0]["coin"][0]["walletBalance"].ToString();
            return Utils.ToDouble(result);
        }

        private static async Task<JToken> MakeRequestAsync(HttpMethod method, string endpoint, Dictionary<string, string>? parameters = null, bool needHeaders = false)
        {
            HttpClient client = new() { BaseAddress = new Uri("https://api.bybit.com") };

            string query = "", signData = "", json = ""; HttpContent body = null;

            if (method == HttpMethod.Get)
            {
                query = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
                signData = query;
            }
            else if (method == HttpMethod.Post)
            {
                json = JsonConvert.SerializeObject(parameters);
                signData = json;
                body = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var uri = $"{endpoint}?{query}";
            var request = new HttpRequestMessage(method, uri) { Content = body };

            if (needHeaders)
            {
                var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var recWindow = "20000";
                var data = $"{timestamp}{apiKey}{recWindow}{signData}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
                byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                var sign = BitConverter.ToString(computedHash).Replace("-", "").ToLower();

                request.Headers.Add("X-BAPI-API-KEY", apiKey);
                request.Headers.Add("X-BAPI-TIMESTAMP", timestamp.ToString());
                request.Headers.Add("X-BAPI-RECV-WINDOW", recWindow);
                request.Headers.Add("X-BAPI-SIGN", sign);
            }

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new Exception($"Http request error code: {response.StatusCode} uri: {uri}\n");
            var responseString = await response.Content.ReadAsStringAsync();
            return ParseResponse(responseString);
        }

        private static JToken ParseResponse(string response)
        {
            var json = JObject.Parse(response);

            var code = Utils.ToDouble(json["retCode"]);
            var msg = json["retMsg"].ToString();

            if (code != 0 || msg != "OK") throw new Exception($"Request error code: {code} message: {msg}\n");
            return json;
        }

        private static JToken ResultList(JToken result) => result["result"]["list"];
    }
}