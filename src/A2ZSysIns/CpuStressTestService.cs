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
                using (var sensors = new CpuSensorSession())
                {
                    progress.Report("CPU stress preflight: checking temperature sensor...");
                    var baselineMetrics = sensors.Read();
                    var baseline = baselineMetrics.MaximumTemperatureC;
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
                                    var metrics = sensors.Read();
                                    if (!metrics.MaximumTemperatureC.HasValue) throw new InvalidOperationException("CPU temperature monitoring was lost; load stopped.");
                                    var temperature = metrics.MaximumTemperatureC.Value;
                                    result.MaximumTemperatureC = Math.Max(result.MaximumTemperatureC ?? temperature, temperature);
                                    var sample = new CpuStressSample {
                                        ElapsedSeconds = (int)timer.Elapsed.TotalSeconds,
                                        TargetLoadPercent = stage.Item1,
                                        TemperatureC = temperature,
                                        ObservedCpuLoadPercent = metrics.CpuLoadPercent,
                                        AverageCoreClockMHz = metrics.AverageCoreClockMHz,
                                        MaximumCoreClockMHz = metrics.MaximumCoreClockMHz,
                                        FanRpm = metrics.FanRpm
                                    };
                                    result.Samples.Add(sample);
                                    EvidenceEngine.Log(report, "CPU stress sample", JsonConvert.SerializeObject(sample));
                                    var clock = metrics.AverageCoreClockMHz.HasValue ? " • " + metrics.AverageCoreClockMHz.Value.ToString("0") + " MHz avg" : " • clock N/A";
                                    progress.Report("CPU stress: " + stage.Item1 + "% target • " + temperature.ToString("0.0") + " °C" + clock + " • " + Math.Min(60, (int)timer.Elapsed.TotalSeconds) + "/60 s");
                                    if (temperature >= MaximumSafeTemperatureC)
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

        private sealed class CpuMetrics
        {
            public double? MaximumTemperatureC;
            public double? CpuLoadPercent;
            public double? AverageCoreClockMHz;
            public double? MaximumCoreClockMHz;
            public double? FanRpm;
        }

        private sealed class CpuSensorSession : IDisposable
        {
            private readonly object _computer;
            private readonly Type _type;
            public CpuSensorSession()
            {
                var asm = Assembly.Load("LibreHardwareMonitorLib");
                _type = asm.GetType("LibreHardwareMonitor.Hardware.Computer", true);
                _computer = Activator.CreateInstance(_type);
                _type.GetProperty("IsCpuEnabled").SetValue(_computer, true, null);
                _type.GetProperty("IsMotherboardEnabled")?.SetValue(_computer, true, null);
                _type.GetProperty("IsControllerEnabled")?.SetValue(_computer, true, null);
                _type.GetMethod("Open").Invoke(_computer, null);
            }

            public CpuMetrics Read()
            {
                var temps = new List<double>(); var loads = new List<double>(); var clocks = new List<double>(); var fans = new List<double>();
                foreach (var hardware in (IEnumerable)_type.GetProperty("Hardware").GetValue(_computer, null)) ReadHardware(hardware, temps, loads, clocks, fans);
                return new CpuMetrics {
                    MaximumTemperatureC = temps.Count == 0 ? (double?)null : temps.Max(),
                    CpuLoadPercent = loads.Count == 0 ? (double?)null : loads.Max(),
                    AverageCoreClockMHz = clocks.Count == 0 ? (double?)null : clocks.Average(),
                    MaximumCoreClockMHz = clocks.Count == 0 ? (double?)null : clocks.Max(),
                    FanRpm = fans.Count == 0 ? (double?)null : fans.Max()
                };
            }

            private static void ReadHardware(object hardware, List<double> temps, List<double> loads, List<double> clocks, List<double> fans)
            {
                var type = hardware.GetType();
                type.GetMethod("Update")?.Invoke(hardware, null);
                var hardwareType = Convert.ToString(type.GetProperty("HardwareType")?.GetValue(hardware, null));
                foreach (var sensor in (IEnumerable)type.GetProperty("Sensors").GetValue(hardware, null))
                {
                    var sensorType = Convert.ToString(sensor.GetType().GetProperty("SensorType").GetValue(sensor, null));
                    var name = Convert.ToString(sensor.GetType().GetProperty("Name").GetValue(sensor, null));
                    var value = sensor.GetType().GetProperty("Value").GetValue(sensor, null);
                    if (value == null) continue;
                    var number = Convert.ToDouble(value);
                    if (hardwareType == "Cpu" && sensorType == "Temperature" && name.IndexOf("Distance", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("TjMax", StringComparison.OrdinalIgnoreCase) < 0 && number > 0 && number < 150) temps.Add(number);
                    else if (hardwareType == "Cpu" && sensorType == "Load" && (name == "CPU Total" || name == "CPU Core Max") && number >= 0 && number <= 100) loads.Add(number);
                    else if (hardwareType == "Cpu" && sensorType == "Clock" && name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 && number > 0) clocks.Add(number);
                    else if (sensorType == "Fan" && number >= 0) fans.Add(number);
                }
                foreach (var child in (IEnumerable)type.GetProperty("SubHardware").GetValue(hardware, null)) ReadHardware(child, temps, loads, clocks, fans);
            }

            public void Dispose() { try { _type.GetMethod("Close").Invoke(_computer, null); } catch { } }
        }

        private sealed class ThermalAbortException : Exception { public ThermalAbortException(string message) : base(message) { } }
    }
}
