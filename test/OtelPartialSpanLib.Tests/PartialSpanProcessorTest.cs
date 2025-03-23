using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Xunit;

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using OpenTelemetry.Logs;

namespace OtelPartialSpanLib.Tests;

public class PartialSpanProcessorTest
{

    [Fact]
    public void TestOtlpBytesSerialization()
    {
        /*
         * - Create a new activity
         * - Store the activity data
         * - Serialize it
         * - Deserialize it
         * - Compare the original activity data with the data from the deserialized bytes.
         */
        ActivitySource SActivitySource = new(this.GetType().Name);

        const string spanName = "activity-test-span";
        using (var activity = SActivitySource.StartActivity(spanName))
		{
			Console.WriteLine("===============> Activity test-span");
		}

    }
}