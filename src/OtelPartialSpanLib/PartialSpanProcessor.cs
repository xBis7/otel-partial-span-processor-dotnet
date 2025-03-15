using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

namespace OtelPartialSpanLib;

public class PartialSpanProcessor<T> : BaseProcessor<T>
    where T : class
{
    
    internal const int DefaultScheduledDelayMilliseconds = 5000;
    internal const int DefaultExporterTimeoutMilliseconds = 30000;

    internal readonly int ScheduledDelayMilliseconds;

    private readonly Thread exporterThread;
    private readonly AutoResetEvent exportTrigger = new(false);
    private readonly ManualResetEvent dataExportedNotification = new(false);
    private readonly ManualResetEvent shutdownTrigger = new(false);

    private readonly BaseExporter<LogRecord> logExporter;
    private readonly ConcurrentDictionary<ActivitySpanId, Activity> activeActivities;
    private readonly ConcurrentQueue<KeyValuePair<ActivitySpanId, Activity>> endedActivities;

    public PartialSpanProcessor(
        BaseExporter<T> exporter, // TODO: It doesn't seem that we need that.
        BaseExporter<LogRecord> logExporter,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds)
    {
        this.logExporter = logExporter;
        // TODO: check if out of ranger.
        // Guard.ThrowIfOutOfRange(scheduledDelayMilliseconds, min: 1);
        // Guard.ThrowIfOutOfRange(exporterTimeoutMilliseconds, min: 0);
        this.ScheduledDelayMilliseconds = scheduledDelayMilliseconds;

        this.exporterThread = new Thread(this.ExporterProc)
        {
            IsBackground = true,
            Name = $"OpenTelemetry-{nameof(PartialSpanProcessor<T>)}-{exporter.GetType().Name}",
        };
        // this.exporterThread.Start();

        this.activeActivities = new ConcurrentDictionary<ActivitySpanId, Activity>();
        this.endedActivities = new ConcurrentQueue<KeyValuePair<ActivitySpanId, Activity>>();
    }

    public override void OnStart(T data)
    {
        if (data is Activity activity)
        {
            Console.WriteLine("x: PartialSpanProcessor.OnStart - " + activity.DisplayName);
        }
    }

    public override void OnEnd(T data)
    {
        if (data is Activity activity)
        {
            Console.WriteLine("x: PartialSpanProcessor.OnEnd - " + activity.DisplayName);
        }
        // this.OnExport(data);
    }

    private void OnExport(T data)
    {
        this.exportTrigger.Set();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.exportTrigger.Dispose();
        this.shutdownTrigger.Dispose();
    }

    private void ExporterProc()
    {
        var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };

        while (true)
        {
            try
            {
                WaitHandle.WaitAny(triggers, this.ScheduledDelayMilliseconds);
                this.Heartbeat();
            }
            catch (ObjectDisposedException)
            {
                // the exporter is somehow disposed before the worker thread could finish its job
                return;
            }
        }
    }

    private void Heartbeat()
    {
        // remove ended activities from active activities
        while (this.endedActivities.TryDequeue(out var activity))
        {
            this.activeActivities.TryRemove(activity.Key, out _);
        }

        // foreach (var keyValuePair in this.activeActivities)
        // {
        //     LogRecord logRecord =
        //         GetLogRecord(keyValuePair.Value, this.GetHeartbeatLogRecordAttributes());
        //     this.logExporter.Export(new Batch<LogRecord>(logRecord));
        // }
    }
    
    // private static LogRecord GetLogRecord(
    //     Activity data,
    //     List<KeyValuePair<string, object?>> logRecordAttributesToBeAdded)
    // {
    //     byte[] buffer = new byte[750000];
    //     var sdkLimitOptions = new SdkLimitOptions();
    //     int writePosition = ProtobufOtlpTraceSerializer
    //         .WriteTraceData(ref buffer, 0, sdkLimitOptions, null, new Batch<Activity>(data));
    //
    //     var logRecord = new LogRecord
    //     {
    //         Timestamp = DateTime.UtcNow,
    //         TraceId = data.TraceId,
    //         SpanId = data.SpanId,
    //         TraceFlags = ActivityTraceFlags.None,
    //         Severity = LogRecordSeverity.Info,
    //         SeverityText = "Info",
    //         Body = Convert.ToBase64String(buffer, 0, writePosition),
    //     };
    //     var logRecordAttributes = GetLogRecordAttributes();
    //     logRecordAttributes.AddRange(logRecordAttributesToBeAdded);
    //     logRecord.Attributes = logRecordAttributes;
    //
    //     return logRecord;
    // }
}
