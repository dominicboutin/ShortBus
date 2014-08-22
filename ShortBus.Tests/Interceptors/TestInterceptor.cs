using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ShortBus.Tests.Interceptors
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ShortBusATAttribute : RequestInterceptAttribute
    {
        public readonly string testString;
        public ShortBusATAttribute(string testString)
        {
            this.testString = testString;
        }

        public override Type[] GetInterceptors()
        {
            return new[] { typeof(TestInterceptor) };
        }
    }

    public class TestInterceptor : RequestInterceptor
    {
        private readonly Stopwatch stopwatch;

        public TestInterceptor()
        {
            stopwatch = new Stopwatch();
        }

        public override void AfterInvoke(MethodInfo requestHandler, object request, Type requestType, object responseData)
        {
            stopwatch.Stop();
            var testString = ((ShortBusATAttribute)(RequestInterceptAttribute)).testString;
            string message = string.Format("Execution of {0} took {1}. Attribute value is {2}",
                requestType.FullName,
                stopwatch.Elapsed,
                testString);

            stopwatch.Reset();
        }

        public override void BeforeInvoke(MethodInfo requestHandler, object request, Type requestType)
        {
            stopwatch.Start();
        }

        public override async Task AfterInvokeAsync(MethodInfo requestHandler, object request, Type requestType, object responseData)
        {
            stopwatch.Stop();
            string message = string.Format("Execution of {0} took {1}.",
                requestType.FullName,
                stopwatch.Elapsed);

            stopwatch.Reset();
        }

        public override async Task BeforeInvokeAsync(MethodInfo requestHandler, object request, Type requestType)
        {
            stopwatch.Start();
        }
    }
}
