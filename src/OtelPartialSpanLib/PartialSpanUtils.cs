using System.Diagnostics;
using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace OtelPartialSpanLib;

public static class PartialSpanUtils
{
    internal static byte[] ConvertActivityToOtlpBytes(Activity activity)
    {
        /*
         * When converting a span to OTLP bytes, the hierarchy should be
         * 
         * _ Resource Spans
         *      |_ Scope Spans
         *          |_ Span1
         *          |_ Span2
         *          |_ etc.
         *
         * Therefore, we have to create a ResourceSpans obj,
         * under that create a ScopeSpans obj and then include the spans.
         */
        
        var resourceSpans = new ResourceSpans
        {
            Resource = new Resource
            {
                // Resource attributes.
                Attributes =
                {
                    new KeyValue
                    {
                        Key = "service.name",
                        Value = new AnyValue { StringValue = "PartialSpanService" }
                    }
                }
            },
        };
        
        var scopeSpans = new ScopeSpans // Older versions refer to this as `InstrumentationLibrary`.
        {
            Scope = new InstrumentationScope
            {
                Name = "PartialSpanScope",
                Version = "1.0.0"
            }
        };

        // Manually set all the fields from the Activity obj to the proto Span obj.
        var otlpSpan = new Span
        {
            /*
             * These are the fields that the Span has.
             * 
             * traceId_ = other.traceId_;
             * spanId_ = other.spanId_;
             * traceState_ = other.traceState_;
             * parentSpanId_ = other.parentSpanId_;
             * flags_ = other.flags_;
             * name_ = other.name_;
             * kind_ = other.kind_;
             * startTimeUnixNano_ = other.startTimeUnixNano_;
             * endTimeUnixNano_ = other.endTimeUnixNano_;
             * attributes_ = other.attributes_.Clone();
             * droppedAttributesCount_ = other.droppedAttributesCount_;
             * events_ = other.events_.Clone();
             * droppedEventsCount_ = other.droppedEventsCount_;
             * links_ = other.links_.Clone();
             * droppedLinksCount_ = other.droppedLinksCount_;
             * status_ = other.status_ != null ? other.status_.Clone() : null;
             * _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
             */
            Name = activity.DisplayName,
            StartTimeUnixNano = (ulong) ToUnixTimeNanoseconds(activity.StartTimeUtc),
            EndTimeUnixNano = (ulong)ToUnixTimeNanoseconds(activity.StartTimeUtc + activity.Duration),
            TraceId = ByteString.CopyFrom(TraceIdToBytes(activity.TraceId)),
            SpanId  = ByteString.CopyFrom(SpanIdToBytes(activity.SpanId)),
        };

        if (activity.ParentSpanId != default)
        {
            otlpSpan.ParentSpanId = ByteString.CopyFrom(SpanIdToBytes(activity.ParentSpanId));
        }

        foreach (var tag in activity.Tags)
        {
            otlpSpan.Attributes.Add(new KeyValue
            {
                Key = tag.Key,
                Value = new AnyValue { StringValue = tag.Value }
            });
        }

        // Add the Span obj to the ScopeSpans obj.
        // Add the ScopeSpans to the ResourceSpans obj.
        // Convert the ResourceSpans obj to a byte array with raw otlp bytes and return it.
        scopeSpans.Spans.Add(otlpSpan);
        resourceSpans.ScopeSpans.Add(scopeSpans);
        byte[] otlpBytes = resourceSpans.ToByteArray();

        return otlpBytes;
    }

    // Helper methods for performing conversions. 
    private static byte[] TraceIdToBytes(ActivityTraceId traceId)
    {
        // TraceId is 16 bytes of binary info, typically displayed as a 32 char hex string.
        Span<byte> buffer = stackalloc byte[16];
        traceId.CopyTo(buffer); 
        return buffer.ToArray();
    }

    private static byte[] SpanIdToBytes(ActivitySpanId spanId)
    {
        // SpanId is 8 bytes of binary info.
        Span<byte> buffer = stackalloc byte[8];
        spanId.CopyTo(buffer);
        return buffer.ToArray();
    }
    
    private static long ToUnixTimeNanoseconds(DateTime dateTime)
    {
        // Use DateTimeOffset to get Unix time in ms, then convert to ns
        var dto = new DateTimeOffset(dateTime, TimeSpan.Zero);
        return dto.ToUnixTimeMilliseconds() * 1_000_000; 
    }
    
}