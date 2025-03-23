using System.Diagnostics;
using Google.Protobuf;
using OpenTelemetry;
using OpenTelemetry.Proto.Trace.V1;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit.Abstractions;

namespace OtelPartialSpanLib.Tests;

public class PartialSpanUtilsTest(ITestOutputHelper testOutputHelper)
{

    [Fact]
    public void TestActivityAfterOtlpBytesSerialization()
    {
        /*
         * - Create a new activity
         * - Serialize it
         * - Deserialize it
         * - Compare the original activity data with the data from the deserialized bytes.
         */

        // The ActivitySource and the ServiceName must match.
        ActivitySource activitySource = new(GetType().Name);
        string serviceName = activitySource.Name;
        const string serviceVersion = "1.0.0";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .AddConsoleExporter()
            .Build();


        const string spanName = "activity-test-span";
        using var activity = activitySource.StartActivity(spanName);

        Assert.NotNull(activity);

        var otlpBytes = PartialSpanUtils.ConvertActivityToOtlpBytes(activity);
        var base64Trace = PartialSpanUtils.ConvertBytesToHexString(otlpBytes);

        Span deserializedSpan = GetDeserializedSpanFromBase64(base64Trace);

        var deserializedTraceId = deserializedSpan.TraceId;
        var deserializedSpanId = deserializedSpan.SpanId;

        var deserializedTraceIdHexStr = ByteStringToHex(deserializedTraceId);
        var deserializedSpanIdHexStr = ByteStringToHex(deserializedSpanId);

        // Convert both of them to lower case hex strings and compare them.
        Assert.Equal(activity.TraceId.ToHexString().ToLower(), deserializedTraceIdHexStr);
        Assert.Equal(activity.SpanId.ToHexString().ToLower(), deserializedSpanIdHexStr);

        // Print just for debugging.
        testOutputHelper.WriteLine("=============== Serialized Otlp ===============");
        testOutputHelper.WriteLine(base64Trace);

        testOutputHelper.WriteLine("=============== Original Activity ===============");
        testOutputHelper.WriteLine("TraceId: " + activity.TraceId.ToHexString());
        testOutputHelper.WriteLine("SpanId: " + activity.SpanId.ToHexString());

        testOutputHelper.WriteLine("=============== Deserialized OtlpBytes ===============");
        testOutputHelper.WriteLine($"TraceId: {deserializedTraceIdHexStr}");
        testOutputHelper.WriteLine($"SpanId: {deserializedSpanIdHexStr}");
    }

    private Span GetDeserializedSpanFromBase64(string base64Encoded)
    {
        byte[] rawBytes = Convert.FromBase64String(base64Encoded);

        ResourceSpans resourceSpans = ResourceSpans.Parser.ParseFrom(rawBytes);

        Assert.True(resourceSpans.ScopeSpans.Count == 1);
        Assert.True(resourceSpans.ScopeSpans[0].Spans.Count == 1);

        return resourceSpans.ScopeSpans[0].Spans[0];
    }

    private static string ByteStringToHex(ByteString byteString)
    {
        return Convert.ToHexStringLower(byteString.ToByteArray());
    }
}
