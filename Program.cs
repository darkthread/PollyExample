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
            //https://blog.darkthread.net/blog/polly-policy-wrap/
            PolicyWrapDemo.Test();
            
            //https://blog.darkthread.net/blog/circuitbreakerpolicy/
            var d = new CircuitBreakerDemo();
            d.Test1();
            d.Test2();
            d.Test3();
        }
    }
}
