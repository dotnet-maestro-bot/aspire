// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using OpenTelemetry.Proto.Metrics.V1;

namespace Aspire.Dashboard.Otlp.Model.MetricValues;

[DebuggerDisplay("Name = {Name}, Values = {Values.Count}")]
public class DimensionScope
{
    public string Name { get; init; }
    public KeyValuePair<string, string>[] Attributes { get; }
    public readonly List<MetricValueBase> Values = new();

    // Used to aid in merging values that are the same in a concurrent environment
    private MetricValueBase? _lastValue;

    public bool IsHistogram => Values.Count > 0 && Values[0] is HistogramValue;

    public DimensionScope(KeyValuePair<string, string>[] attributes)
    {
        Attributes = attributes;
        var name = Attributes.ConcatProperties();
        Name = name != null && name.Length > 0 ? name : "no-dimensions";
    }

    /// <summary>
    /// Compares and updates the timespan for metrics if they are unchanged.
    /// </summary>
    /// <param name="d">Metric value to merge</param>
    public void AddPointValue(NumberDataPoint d)
    {
        var start = OtlpHelpers.UnixNanoSecondsToDateTime(d.StartTimeUnixNano);
        var end = OtlpHelpers.UnixNanoSecondsToDateTime(d.TimeUnixNano);

        if (d.ValueCase == NumberDataPoint.ValueOneofCase.AsInt)
        {
            var value = d.AsInt;
            var lastLongValue = _lastValue as MetricValue<long>;
            if (lastLongValue is not null && lastLongValue.Value == value)
            {
                lastLongValue.End = end;
                Interlocked.Increment(ref lastLongValue.Count);
            }
            else
            {
                if (lastLongValue is not null)
                {
                    start = lastLongValue.End;
                }
                _lastValue = new MetricValue<long>(d.AsInt, start, end);
                Values.Add(_lastValue);
            }
        }
        else if (d.ValueCase == NumberDataPoint.ValueOneofCase.AsDouble)
        {
            var lastDoubleValue = _lastValue as MetricValue<double>;
            if (lastDoubleValue is not null && lastDoubleValue.Value == d.AsDouble)
            {
                lastDoubleValue.End = end;
                Interlocked.Increment(ref lastDoubleValue.Count);
            }
            else
            {
                if (lastDoubleValue is not null)
                {
                    start = lastDoubleValue.End;
                }
                _lastValue = new MetricValue<double>(d.AsDouble, start, end);
                Values.Add(_lastValue);
            }
        }
    }

    public void AddHistogramValue(HistogramDataPoint h)
    {
        var start = OtlpHelpers.UnixNanoSecondsToDateTime(h.StartTimeUnixNano);
        var end = OtlpHelpers.UnixNanoSecondsToDateTime(h.TimeUnixNano);

        var lastHistogramValue = _lastValue as HistogramValue;
        if (lastHistogramValue is not null && lastHistogramValue.Count == h.Count)
        {
            lastHistogramValue.End = end;
        }
        else
        {
            if (lastHistogramValue is not null)
            {
                start = lastHistogramValue.End;
            }
            _lastValue = new HistogramValue(h.BucketCounts, h.Sum, h.Count, start, end, h.ExplicitBounds);
            Values.Add(_lastValue);
        }
    }
}
