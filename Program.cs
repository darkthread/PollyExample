using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Polly;
using System.Text.Json;

namespace PollyDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var data = QueryData();
            var options = new JsonSerializerOptions {
                WriteIndented = true
            };
            Console.WriteLine(JsonSerializer.Serialize(data, options));
        }

        public class Entry
        {
            public Guid Id { get; set; }
            public string Subject { get; set; }
        }
        static IEnumerable<Entry> Get3rdPtyData(string srcName, int delayTime)
        {
            Task.Delay(delayTime * 1000).Wait();
            return Enumerable.Range(1, 1).Select(o =>
                 new Entry
                 {
                     Id = Guid.NewGuid(),
                     Subject = $"Data from ExtraData[{srcName}] {DateTime.Now.Ticks % 100000:00000}"
                 });
        }

        static Dictionary<string, Func<IEnumerable<Entry>>> extDataJobs = 
            new Dictionary<string, Func<IEnumerable<Entry>>>
            {
                ["SrcA"] = () => Get3rdPtyData("A", 3),
                ["SrcB"] = () => Get3rdPtyData("B", 8),
                ["SrcC"] = () => { throw new ApplicationException("Error"); }
            };
        static IEnumerable<Entry> QueryData()
        {
            var list = new List<Entry>();
            //Simulate data from local
            list.Add(new Entry
            {
                Id = Guid.NewGuid(),
                Subject = "Data from local service"
            });

            Parallel.ForEach(extDataJobs.Keys, (src) =>
            {
                var policyTimeout = 
                    Policy.Timeout(TimeSpan.FromSeconds(5), Polly.Timeout.TimeoutStrategy.Pessimistic);
                var policyTimeoutFallback = Policy<IEnumerable<Entry>>
                    .Handle<Polly.Timeout.TimeoutRejectedException>()
                    .Fallback(() =>
                        new List<Entry>() {
                            new Entry
                            {
                                Id = Guid.NewGuid(),
                                Subject = $"Warning: [{src}] API timeout"
                            }
                    });
                var policyFail = Policy<IEnumerable<Entry>>.Handle<Exception>().Fallback(() =>
                {
                    return new List<Entry>() {
                        new Entry
                        {
                            Id = Guid.NewGuid(),
                            Subject = $"Warning: [{src}] API failed"
                        }
                    };
                });

                var policyWrap = policyFail.Wrap(policyTimeoutFallback).Wrap(policyTimeout);

                var extData = policyWrap.Execute(() =>
                {
                    return extDataJobs[src]();
                });
                lock (list)
                {
                    list.AddRange(extData);
                }
            });

            return list;
        }

    }
}
