using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Polly;
using System.Text.Json;
using System.Threading;
using Polly.CircuitBreaker;

namespace PollyDemo
{
    public class BreakNowException : Exception { }
    public class CircuitBreakerDemo
    {
        public enum RespModes
        {
            Normal,
            ThrowOtherEx,
            ThrowBrkEx
        }

        string DoJob(RespModes respMode)
        {
            switch (respMode)
            {
                case RespModes.ThrowOtherEx:
                    throw new ApplicationException();
                case RespModes.ThrowBrkEx:
                    throw new BreakNowException();
            }
            return $"> 執行成功 {DateTime.Now:mm:ss}";
        }

        public CircuitBreakerDemo()
        {
        }

        void Write(string msg, ConsoleColor color = ConsoleColor.White)
        {
            var bak = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ForegroundColor = bak;
        }

        void Log(CircuitBreakerPolicy<string> policy, string msg) =>
            Write($"{DateTime.Now:mm:ss} State = {policy.CircuitState} ({msg})", ConsoleColor.Yellow);

        public void Test1()
        {
            Write("\n**** Simple Test ****", ConsoleColor.Cyan);
            //標準測試，錯兩次斷路 5 秒
            var pBreaker = Policy<string>.Handle<Exception>().CircuitBreaker(2, TimeSpan.FromSeconds(5));
            var pFallback =
                Policy<string>
                    .Handle<Exception>()
                    .Fallback((delgRes, context, cancelToken) =>
                    {
                        var ex = delgRes.Exception;
                        return $"> {ex.GetType().Name}/{ex.InnerException?.GetType().Name} {DateTime.Now:mm:ss}";
                    },
                    onFallback: (ex, context) => { });
            var p = Policy.Wrap(pFallback, pBreaker);

            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
            Log(pBreaker, "BreakNowExcpetion 前");
            Console.WriteLine(p.Execute(() => DoJob(RespModes.ThrowBrkEx)));
            Log(pBreaker, $"1st BreakNowExcpetion");
            Console.WriteLine(p.Execute(() => DoJob(RespModes.ThrowBrkEx)));
            Log(pBreaker, $"2nd BreakNowExcpetion，預計斷至 {DateTime.Now.AddSeconds(5):mm:ss}");
            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
            Thread.Sleep(1000);
            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
            Thread.Sleep(5000);
            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
        }

        public void Test2()
        {
            Write("\n**** State Test ****", ConsoleColor.Cyan);
            // 測試重點: 
            // 1. Half-Open 遇非指定錯誤會繼續 Half-Open
            // 2. Half-Open 測試失敗會再延長
            var pBreaker = Policy<string>.Handle<BreakNowException>()
                .CircuitBreaker(1, TimeSpan.FromSeconds(2));
            var pFallback =
                Policy<string>
                    .Handle<Exception>()
                    .Fallback((delgRes, context, cancelToken) =>
                    {
                        var ex = delgRes.Exception;
                        return $"> {ex.GetType().Name}/{ex.InnerException?.GetType().Name} {DateTime.Now:mm:ss}";
                    },
                    onFallback: (ex, context) => { });
            var p = Policy.Wrap(pFallback, pBreaker);

            Console.WriteLine(p.Execute(() => DoJob(RespModes.ThrowBrkEx)));
            // Open
            Log(pBreaker, $"BreakNowExcpetion，預計斷至 {DateTime.Now.AddSeconds(2):mm:ss}");
            Thread.Sleep(2000);
            // Half-Open
            Log(pBreaker, $"durationOfBreak 結束");
            // Trial attempt 
            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
            Log(pBreaker, $"Half-Open 後下一次執行成功");
            // Break it again
            Console.WriteLine(p.Execute(() => DoJob(RespModes.ThrowBrkEx)));
            Log(pBreaker, $"再 BreakNowExcpetion，預計斷至 {DateTime.Now.AddSeconds(2):mm:ss}");
            Thread.Sleep(2000);
            Log(pBreaker, $"durationOfBreak 結束");
            // Trial attempt 
            Console.WriteLine(p.Execute(() => DoJob(RespModes.ThrowBrkEx)));
            Log(pBreaker, $"Half-Open 後下一次執行失敗，預計斷至 {DateTime.Now.AddSeconds(2):mm:ss}");
            Thread.Sleep(2000);
            // Half-Open
            Log(pBreaker, $"durationOfBreak 結束");
            // Trial attempt 
            Console.WriteLine(p.Execute(() => DoJob(RespModes.ThrowOtherEx)));
            // Unhandled Exception -> Half-Open
            Log(pBreaker, $"丟出非目標例外，維持 Half-Open");
            // Half-Open 的其他執行收到錯誤
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
                Log(pBreaker, $"每秒呼叫一次 {(i + 1)}/3");
                Thread.Sleep(1000);
            }
        }

        public void Test3()
        {
            Write("\n**** Isolate Test ****", ConsoleColor.Cyan);
            //標準測試，錯兩次斷路 5 秒
            var pBreaker = Policy<string>.Handle<Exception>().CircuitBreaker(10, TimeSpan.FromSeconds(1));
            var pFallback =
                Policy<string>
                    .Handle<Exception>()
                    .Fallback((delgRes, context, cancelToken) =>
                    {
                        var ex = delgRes.Exception;
                        return $"> {ex.GetType().Name}/{ex.InnerException?.GetType().Name} {DateTime.Now:mm:ss}";
                    },
                    onFallback: (ex, context) => { });
            var p = Policy.Wrap(pFallback, pBreaker);

            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
            Log(pBreaker, "正常運作");
            pBreaker.Isolate();
            Log(pBreaker, "呼叫 Isolate()");
            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
            Thread.Sleep(2000);
            Log(pBreaker, "超過 durationOfBreak 也不會解除");
            Console.WriteLine(p.Execute(() => DoJob(RespModes.ThrowBrkEx)));
            pBreaker.Reset();
            Log(pBreaker, "呼叫 Reset()");
            Console.WriteLine(p.Execute(() => DoJob(RespModes.Normal)));
        }

    }
}