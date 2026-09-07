using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace A2ZSysIns
{
    public static class CpuStressTestService
    {
        private const double MaximumSafeTemperatureC = 90.0;
        private const double MaximumPreflightTemperatureC = 80.0;

        public static async Task<CpuStressResult> RunAsync(InspectionReport report, CancellationToken cancellation, IProgress<string> progress)
        {
            var result = new CpuStressResult { StartedAt = DateTime.Now, LogicalWorkers = Environment.ProcessorCount,
                PlannedDurationSeconds = 60, Status = "Starting" };
            report.CpuStressTest = result;
            EvidenceEngine.Log(report, "CPU stress test requested", "60-second staged test; workers=" + result.LogicalWorkers +
                "; preflight limit=" + MaximumPreflightTemperatureC + " C; abort limit=" + MaximumSafeTemperatureC + " C");

            var timer = Stopwatch.StartNew();
            try
            {
                using (var temperatures = new CpuTemperatureSession())
                {
                    progress.Report("CPU stress preflight: checking temperature sensor...");
                    var baseline = temperatures.ReadMaximumCpuTemperature();
                    if (!baseline.HasValue)
                    {
                        result.Status = "Refused";
                        result.StopReason = "Cannot safely run: an actual CPU package/core temperature sensor is unavailable.";
                        return Finish(report, result, timer);
                    }
                    result.BaselineTemperatureC = baseline;
                    result.MaximumTemperatureC = baseline;
                    if (baseline.Value >= MaximumPreflightTemperatureC)
                    {
                        result.Status = "Refused";
                        result.StopReason = "Cannot safely run: preflight CPU temperature is " + baseline.Value.ToString("0.0") + " C.";
                        return Finish(report, result, timer);
                    }

                    var stages = new[] { Tuple.Create(40, 15), Tuple.Create(70, 15), Tuple.Create(100, 30) };
                    foreach (var stage in stages)
                    {
                        using (var stageCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                        {
                            var workers = StartWorkers(stage.Item1, stageCancel.Token, result);
                            try
                            {
                                for (var second = 0; second < stage.Item2; second++)
                                {
                                    cancellation.ThrowIfCancellationRequested();
                                    await Task.Delay(1000, cancellation);
                                    var temperature = temperatures.ReadMaximumCpuTemperature();
                                    if (!temperature.HasValue) throw new InvalidOperationException("CPU temperature monitoring was lost; load stopped.");
                                    result.MaximumTemperatureC = Math.Max(result.MaximumTemperatureC ?? temperature.Value, temperature.Value);
                                    result.Samples.Add(new CpuStressSample { ElapsedSeconds = (int)timer.Elapsed.TotalSeconds,
                                        TargetLoadPercent = stage.Item1, TemperatureC = temperature.Value });
                                    EvidenceEngine.Log(report, "CPU stress sample", JsonConvert.SerializeObject(result.Samples.Last()));
                                    progress.Report("CPU stress: " + stage.Item1 + "% target • " + temperature.Value.ToString("0.0") +
                                        " °C • " + Math.Min(60, (int)timer.Elapsed.TotalSeconds) + "/60 s");
                                    if (temperature.Value >= MaximumSafeTemperatureC)
                                        throw new ThermalAbortException("CPU reached the " + MaximumSafeTemperatureC.ToString("0") + " C safety limit.");
                                }
                            }
                            finally
                            {
                                stageCancel.Cancel();
                                try { await Task.WhenAll(workers); } catch (OperationCanceledException) { }
                            }
                        }
                    }
                }
                result.Status = "Completed";
                result.StopReason = "Completed without reaching the thermal safety limit. This short test does not prove long-term stability.";
            }
            catch (OperationCanceledException)
            {
                result.Status = "Cancelled";
                result.StopReason = "Cancelled by the technician.";
            }
            catch (ThermalAbortException ex)
            {
                result.Status = "Thermal abort";
                result.StopReason = ex.Message;
            }
            catch (Exception ex)
            {
                result.Status = "Aborted";
                result.StopReason = ex.GetBaseException().Message;
            }
            return Finish(report, result, timer);
        }

        private static CpuStressResult Finish(InspectionReport report, CpuStressResult result, Stopwatch timer)
        {
            timer.Stop(); result.CompletedAt = DateTime.Now; result.ActualDurationSeconds = (int)Math.Ceiling(timer.Elapsed.TotalSeconds);
            EvidenceEngine.Record(report, "CPU staged stress test", "Integrated CPU load + LibreHardwareMonitor",
                result.Status == "Completed" ? "Measured" : result.Status == "Refused" ? "Unavailable" : "Partial",
                result.Status + ": " + result.StopReason, JsonConvert.SerializeObject(result));
            EvidenceEngine.Log(report, "CPU stress test finished", JsonConvert.SerializeObject(result));
            return result;
        }

        private static Task[] StartWorkers(int targetPercent, CancellationToken token, CpuStressResult result)
        {
            return Enumerable.Range(0, Math.Max(1, Environment.ProcessorCount)).Select(_ => Task.Run(() =>
            {
                var cycle = Stopwatch.StartNew();
                while (!token.IsCancellationRequested)
                {
                    cycle.Restart();
                    var workMilliseconds = targetPercent;
                    while (cycle.ElapsedMilliseconds < workMilliseconds && !token.IsCancellationRequested)
                    {
                        double value = 0;
                        for (var i = 1; i < 4000; i++) value += Math.Sqrt(i) * Math.Sin(i);
                        Interlocked.Add(ref result.WorkIterations, 4000);
                        GC.KeepAlive(value);
                    }
                    var sleep = 100 - (int)cycle.ElapsedMilliseconds;
                    if (sleep > 0) token.WaitHandle.WaitOne(sleep);
                }
                token.ThrowIfCancellationRequested();
            }, token)).ToArray();
        }

        private sealed class CpuTemperatureSession : IDisposable
        {
            private readonly object _computer;
            private readonly Type _type;
            public CpuTemperatureSession()
            {
                var asm = Assembly.Load("LibreHardwareMonitorLib");
                _type = asm.GetType("LibreHardwareMonitor.Hardware.Computer", true);
                _computer = Activator.CreateInstance(_type);
                _type.GetProperty("IsCpuEnabled").SetValue(_computer, true, null);
                _type.GetMethod("Open").Invoke(_computer, null);
            }
            public double? ReadMaximumCpuTemperature()
            {
                var values = new List<double>();
                foreach (var hardware in (IEnumerable)_type.GetProperty("Hardware").GetValue(_computer, null)) Read(hardware, values);
                return values.Count == 0 ? (double?)null : values.Max();
            }
            private static void Read(object hardware, List<double> values)
            {
                var type = hardware.GetType();
                type.GetMethod("Update")?.Invoke(hardware, null);
                var hardwareType = Convert.ToString(type.GetProperty("HardwareType")?.GetValue(hardware, null));
                if (hardwareType == "Cpu")
                foreach (var sensor in (IEnumerable)type.GetProperty("Sensors").GetValue(hardware, null))
                {
                    var sensorType = Convert.ToString(sensor.GetType().GetProperty("SensorType").GetValue(sensor, null));
                    var name = Convert.ToString(sensor.GetType().GetProperty("Name").GetValue(sensor, null));
                    var value = sensor.GetType().GetProperty("Value").GetValue(sensor, null);
                    if (sensorType == "Temperature" && value != null && name.IndexOf("Distance", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("TjMax", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        var number = Convert.ToDouble(value);
                        if (number > 0 && number < 150) values.Add(number);
                    }
                }
                foreach (var child in (IEnumerable)type.GetProperty("SubHardware").GetValue(hardware, null)) Read(child, values);
            }
            public void Dispose() { try { _type.GetMethod("Close").Invoke(_computer, null); } catch { } }
        }

        private sealed class ThermalAbortException : Exception { public ThermalAbortException(string message) : base(message) { } }
    }
}
