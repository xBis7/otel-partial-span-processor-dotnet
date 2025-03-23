using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;
using Microsoft.Extensions.Logging;

namespace OtelPartialSpanLib;

public class PartialSpanProcessor<T> : BaseProcessor<T>
    where T : class
{
    private const int DefaultScheduledDelayMilliseconds = 5000;
    private const int DefaultExporterTimeoutMilliseconds = 30000;

    private readonly int ScheduledDelayMilliseconds;

    private readonly Thread exporterThread;
    private readonly AutoResetEvent exportTrigger = new(false);
    private readonly ManualResetEvent dataExportedNotification = new(false);
    private readonly ManualResetEvent shutdownTrigger = new(false);

    private readonly ConcurrentDictionary<ActivitySpanId, Activity> activeActivities;
    private readonly ConcurrentQueue<KeyValuePair<ActivitySpanId, Activity>> endedActivities;
    
    private readonly ILogger<T> _logger;

    public PartialSpanProcessor(
        ILogger<T> logger,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds) // TODO: we don't need this.
    {
        this._logger = logger;
        // TODO: check if out of range.
        // Guard.ThrowIfOutOfRange(scheduledDelayMilliseconds, min: 1);
        // Guard.ThrowIfOutOfRange(exporterTimeoutMilliseconds, min: 0);
        this.ScheduledDelayMilliseconds = scheduledDelayMilliseconds;

        this.exporterThread = new Thread(this.ExporterProc)
        {
            IsBackground = true,
            Name = $"OpenTelemetry-{nameof(PartialSpanProcessor<T>)}-ILogger",
        };
        this.exporterThread.Start();

        this.activeActivities = new ConcurrentDictionary<ActivitySpanId, Activity>();
        this.endedActivities = new ConcurrentQueue<KeyValuePair<ActivitySpanId, Activity>>();
    }

    public override void OnStart(T data)
    {
        if (data is not Activity activity) return;

        _logger.LogDebug("PartialSpanProcessor.OnStart - " + activity.DisplayName);
        activeActivities.TryAdd(activity.SpanId, activity);
    }

    public override void OnEnd(T data)
    {
        if (data is not Activity activity) return;

        // this.TryExport(data);
        //
        // var logRecordAttributes = new List<KeyValuePair<string, object?>>
        // {
        //     new("partial.event", "stop"),
        // };
        // var logRecord = GetLogRecord(data, logRecordAttributes);
        // this.logExporter.Export(new Batch<LogRecord>(logRecord));

        _logger.LogDebug("PartialSpanProcessor.OnEnd - " + activity.DisplayName);
        endedActivities.Enqueue(new KeyValuePair<ActivitySpanId, Activity>(activity.SpanId, activity));
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
        // If it can be dequeued then it also needs to be removed from the active activities.
        while (endedActivities.TryDequeue(out var activity))
        {
            activeActivities.TryRemove(activity.Key, out _);
        }

        foreach (var keyValuePair in activeActivities)
        {
            LogOtlpBytesAsALogRecord(keyValuePair.Value);
        }
    }
    
    private void LogOtlpBytesAsALogRecord(Activity data)
    {
        var otlpBytes = PartialSpanUtils.ConvertActivityToOtlpBytes(data);
        var base64Trace = Convert.ToBase64String(otlpBytes);

        // TODO: it might be better to set these attributes on the activity.
        //  This is setting the attributes on the LogRecord.
        //  It depends on how the collector is handling them.
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["span.type"] = "partial",
                   ["partial.event"] = "heartbeat",
                   ["partial.frequency"] = ScheduledDelayMilliseconds + "ms"
               }))
        {
            _logger.LogInformation(base64Trace);
        }
    }
}
