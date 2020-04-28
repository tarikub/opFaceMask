using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace fnOpFaceMask.Simulator
{
    internal class Program
    {
        private Timer _tm = null;
        private AutoResetEvent _autoEvent = null;
        private List<Address> _randomAddresses = null;
        private static Random _rnd;

        private int _counter = 0;

        private static void Main(string[] args)
        {
            Program p = new Program();
            _rnd = new Random();
            p.StartTimer();
        }

        public void StartTimer()
        {
            _autoEvent = new AutoResetEvent(false);
            _tm = new Timer(Execute, _autoEvent, 10000, 10000);

            using (StreamReader file = File.OpenText(@"rrad/addresses-us-100.json"))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                JObject fullList = (JObject)JToken.ReadFrom(reader);
                _randomAddresses = fullList.First.First.ToObject<List<Address>>();
            }

            Console.Read();
        }

        public void Execute(Object stateInfo)
        {
            if (_counter < 10)
            {
                var from = _rnd.Next(100000000, 1000000000).ToString();
                var to = _rnd.Next(100000000, 1000000000).ToString();
                var startTask = Task.Run(async () =>
                {
                    var op = "start";
                    var keyValues = GetParams(from, to, op);
                    Console.WriteLine($"SMS -- from: {from} to: {to} message {op}");
                    await SendRequest(keyValues);
                });

                startTask.ContinueWith(antecedent =>
                {
                    var idxRandomAddress = (new Random()).Next(0, 99);
                    var randomAddress = _randomAddresses[idxRandomAddress].FullAddress();

                    Task.Run(async () =>
                    {
                        var keyValues = GetParams(from, to, randomAddress);
                        Console.WriteLine($"SMS -- from: {from} to: {to} message {randomAddress}");
                        await SendRequest(keyValues);
                    });
                });

                _counter++;
                return;
            }
        }

        private static List<KeyValuePair<string, string>> GetParams(string from, string to, string operation)
        {
            var keyValues = new List<KeyValuePair<string, string>>();
            keyValues.Add(new KeyValuePair<string, string>("from", from));
            keyValues.Add(new KeyValuePair<string, string>("to", to));
            keyValues.Add(new KeyValuePair<string, string>("body", operation));
            return keyValues;
        }

        private static async Task SendRequest(List<KeyValuePair<string, string>> keyValues)
        {
            var requestUrl = $"http://localhost:7071";
            var client = new HttpClient();
            client.BaseAddress = new Uri(requestUrl);
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/Run");
            request.Content = new FormUrlEncodedContent(keyValues);
            var response = await client.SendAsync(request);
        }
    }
}