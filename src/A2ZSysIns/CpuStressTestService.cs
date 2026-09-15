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
        private const int SampleIntervalMilliseconds = AdaptiveTelemetrySampler.SafetyPollMilliseconds;
        private const int PreflightSamples = 6;
        private const int StressBaselineSamples = 5;
        private const double MaximumPreflightTemperatureC = CpuStressSafetyMonitor.PreflightTemperatureC;

        public static Task<CpuStressResult> RunAsync(InspectionReport report, CancellationToken cancellation, IProgress<string> progress) => RunAsync(report, cancellation, progress, null);

        public static async Task<CpuStressResult> RunAsync(InspectionReport report, CancellationToken cancellation, IProgress<string> progress, IProgress<CpuStressSample> sampleProgress)
        {
            var result = new CpuStressResult { StartedAt = DateTime.Now, LogicalWorkers = Environment.ProcessorCount, PlannedDurationSeconds = 60, Status = "Starting" };
            report.CpuStressTest = result;
            EvidenceEngine.Log(report, "CPU stress test requested", "60-second staged test; workers=" + result.LogicalWorkers + "; safety poll=" + SampleIntervalMilliseconds + " ms; per-sensor cadence learning enabled; hard temperature limit=" + CpuStressSafetyMonitor.HardTemperatureC + " C");
            var timer = Stopwatch.StartNew();
            try
            {
                using (var sensors = new CpuSensorSession())
                {
                    progress.Report("CPU stress preflight: learning sensor update cadence...");
                    var safety = new CpuStressSafetyMonitor(); var cadence = new AdaptiveTelemetrySampler();
                    var baselineClocks = new List<double>(); var baselineTemperatures = new List<double>();
                    for (var i = 0; i < PreflightSamples; i++)
                    {
                        cancellation.ThrowIfCancellationRequested(); var metrics = sensors.Read(); var capturedAt = DateTime.Now;
                        metrics.PollIntervalMilliseconds = cadence.Observe(metrics, capturedAt, false);
                        var decision = safety.Evaluate(metrics, false);
                        if (decision.Abort) { result.Status = "Refused"; result.StopReason = decision.Reason; return Finish(report, result, timer); }
                        if (!metrics.AverageCoreClockMHz.HasValue || !metrics.CpuLoadPercent.HasValue) { result.Status = "Refused"; result.StopReason = "Cannot safely run: CPU clock and CPU load telemetry must both be available before applying stress."; return Finish(report, result, timer); }
                        baselineClocks.Add(metrics.AverageCoreClockMHz.Value); baselineTemperatures.Add(metrics.TemperatureC.Value);
                        await Task.Delay(metrics.PollIntervalMilliseconds, cancellation);
                    }
                    var baselineClock = Median(baselineClocks); var baselineTemperature = Median(baselineTemperatures);
                    var clockRangeRatio = (baselineClocks.Max() - baselineClocks.Min()) / Math.Max(1.0, baselineClock);
                    if (clockRangeRatio > 0.50) { result.Status = "Refused"; result.StopReason = "Cannot safely run: CPU clock telemetry was unstable during preflight (range=" + (clockRangeRatio * 100).ToString("0") + "%)."; return Finish(report, result, timer); }
                    if (baselineTemperature >= MaximumPreflightTemperatureC) { result.Status = "Refused"; result.StopReason = "Cannot safely run: preflight CPU temperature is " + baselineTemperature.ToString("0.0") + " °C."; return Finish(report, result, timer); }
                    result.BaselineTemperatureC = baselineTemperature; result.MaximumTemperatureC = baselineTemperature; result.BaselineClockMHz = baselineClock; result.MinimumObservedClockMHz = baselineClock; result.MaximumObservedClockMHz = baselineClock;

                    var stages = new[] { Tuple.Create(40, 15), Tuple.Create(70, 15), Tuple.Create(100, 30) }; var stressTimer = Stopwatch.StartNew();
                    var stressBaselineClocks = new List<double>(); var stressBaselineEstablished = false;
                    foreach (var stage in stages)
                    {
                        using (var stageCancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                        {
                            var workers = StartWorkers(stage.Item1, stageCancel.Token, result); var stageTimer = Stopwatch.StartNew();
                            try
                            {
                                var nextUiUpdate = 0L;
                                while (stageTimer.ElapsedMilliseconds < stage.Item2 * 1000L)
                                {
                                    cancellation.ThrowIfCancellationRequested(); var metrics = sensors.Read(); var capturedAt = DateTime.Now;
                                    metrics.PollIntervalMilliseconds = cadence.Observe(metrics, capturedAt, true);
                                    var decision = safety.Evaluate(metrics, true); if (decision.Abort) throw new SafetyAbortException(decision.Reason);
                                    if (!stressBaselineEstablished && metrics.AverageCoreClockMHz.HasValue)
                                    {
                                        stressBaselineClocks.Add(metrics.AverageCoreClockMHz.Value);
                                        if (stressBaselineClocks.Count >= StressBaselineSamples) { safety.SetStressBaseline(stressBaselineClocks); stressBaselineEstablished = true; result.BaselineClockMHz = safety.BaselineClockMHz; EvidenceEngine.Log(report, "CPU stress baseline established", "Load baseline clock=" + safety.BaselineClockMHz.Value.ToString("0") + " MHz"); }
                                    }
                                    var elapsedMs = (int)Math.Min(int.MaxValue, stressTimer.ElapsedMilliseconds);
                                    var sample = new CpuStressSample { ElapsedSeconds = elapsedMs / 1000, ElapsedMilliseconds = elapsedMs, CapturedAt = capturedAt, TargetLoadPercent = stage.Item1, TemperatureC = metrics.TemperatureC.Value, ObservedCpuLoadPercent = metrics.CpuLoadPercent, AverageCoreClockMHz = metrics.AverageCoreClockMHz, MaximumCoreClockMHz = metrics.MaximumCoreClockMHz, FanRpm = metrics.FanRpm, MemoryUsedPercent = metrics.MemoryUsedPercent, GpuLoadPercent = metrics.GpuLoadPercent, GpuTemperatureC = metrics.GpuTemperatureC, TelemetryFreshness = cadence.DescribeFreshness(metrics), TelemetryPollIntervalMilliseconds = metrics.PollIntervalMilliseconds, SafetySampleValid = decision.SampleValid, SafetyAssessment = decision.Assessment };
                                    result.Samples.Add(sample); result.MaximumTemperatureC = Math.Max(result.MaximumTemperatureC ?? sample.TemperatureC, sample.TemperatureC);
                                    if (sample.AverageCoreClockMHz.HasValue) { result.MinimumObservedClockMHz = Math.Min(result.MinimumObservedClockMHz ?? sample.AverageCoreClockMHz.Value, sample.AverageCoreClockMHz.Value); result.MaximumObservedClockMHz = Math.Max(result.MaximumObservedClockMHz ?? sample.AverageCoreClockMHz.Value, sample.AverageCoreClockMHz.Value); }
                                    sampleProgress?.Report(sample); EvidenceEngine.Log(report, "CPU stress sample", JsonConvert.SerializeObject(sample));
                                    if (elapsedMs >= nextUiUpdate) { nextUiUpdate = elapsedMs + 500; var clock = sample.AverageCoreClockMHz.HasValue ? " • " + sample.AverageCoreClockMHz.Value.ToString("0") + " MHz avg" : " • clock N/A"; var gpu = sample.GpuLoadPercent.HasValue ? " • GPU " + sample.GpuLoadPercent.Value.ToString("0") + "%" : " • GPU N/A"; progress.Report("CPU stress: " + stage.Item1 + "% target • " + sample.TemperatureC.ToString("0.0") + " °C" + clock + " • " + sample.ObservedCpuLoadPercent.GetValueOrDefault().ToString("0") + "% CPU • RAM " + (sample.MemoryUsedPercent.HasValue ? sample.MemoryUsedPercent.Value.ToString("0") + "%" : "N/A") + gpu + " • " + Math.Min(60, elapsedMs / 1000) + "/60 s"); }
                                    await Task.Delay(Math.Max(AdaptiveTelemetrySampler.MinimumPollMilliseconds, Math.Min(SampleIntervalMilliseconds, metrics.PollIntervalMilliseconds)), cancellation);
                                }
                            }
                            finally { stageTimer.Stop(); stageCancel.Cancel(); try { await Task.WhenAll(workers); } catch (OperationCanceledException) { } }
                        }
                    }
                }
                result.Status = "Completed"; result.StopReason = "Completed without reaching a safety abort condition. This short test does not prove long-term stability.";
            }
            catch (OperationCanceledException) { result.Status = "Cancelled"; result.StopReason = "Cancelled by the technician."; }
            catch (SafetyAbortException ex) { result.Status = "Safety abort"; result.StopReason = ex.Message; }
            catch (Exception ex) { result.Status = "Aborted"; result.StopReason = ex.GetBaseException().Message; }
            return Finish(report, result, timer);
        }

        private static CpuStressResult Finish(InspectionReport report, CpuStressResult result, Stopwatch timer)
        {
            timer.Stop(); result.CompletedAt = DateTime.Now; result.ActualDurationSeconds = (int)Math.Ceiling(timer.Elapsed.TotalSeconds);
            EvidenceEngine.Record(report, "CPU staged stress test", "Integrated CPU load + LibreHardwareMonitor + adaptive high-frequency safety telemetry", result.Status == "Completed" ? "Measured" : result.Status == "Refused" ? "Unavailable" : "Partial", result.Status + ": " + result.StopReason, JsonConvert.SerializeObject(result));
            EvidenceEngine.Log(report, "CPU stress test finished", JsonConvert.SerializeObject(result)); return result;
        }

        private static Task[] StartWorkers(int targetPercent, CancellationToken token, CpuStressResult result)
        {
            return Enumerable.Range(0, Math.Max(1, Environment.ProcessorCount)).Select(_ => Task.Run(() => { var cycle = Stopwatch.StartNew(); while (!token.IsCancellationRequested) { cycle.Restart(); var workMilliseconds = targetPercent; while (cycle.ElapsedMilliseconds < workMilliseconds && !token.IsCancellationRequested) { double value = 0; for (var i = 1; i < 4000; i++) value += Math.Sqrt(i) * Math.Sin(i); Interlocked.Add(ref result.WorkIterations, 4000); GC.KeepAlive(value); } var sleep = 100 - (int)cycle.ElapsedMilliseconds; if (sleep > 0) token.WaitHandle.WaitOne(sleep); } token.ThrowIfCancellationRequested(); }, token)).ToArray();
        }

        private sealed class CpuMetrics : CpuSafetyMetrics { }
        private sealed class CpuSensorSession : IDisposable
        {
            private readonly object _computer; private readonly Type _type;
            public CpuSensorSession()
            {
                var asm = Assembly.Load("LibreHardwareMonitorLib"); _type = asm.GetType("LibreHardwareMonitor.Hardware.Computer", true); _computer = Activator.CreateInstance(_type);
                _type.GetProperty("IsCpuEnabled").SetValue(_computer, true, null); _type.GetProperty("IsMemoryEnabled")?.SetValue(_computer, true, null); _type.GetProperty("IsGpuEnabled")?.SetValue(_computer, true, null); _type.GetProperty("IsMotherboardEnabled")?.SetValue(_computer, true, null); _type.GetProperty("IsControllerEnabled")?.SetValue(_computer, true, null); _type.GetMethod("Open").Invoke(_computer, null);
            }
            public CpuMetrics Read()
            {
                var temps = new List<double>(); var loads = new List<double>(); var clocks = new List<double>(); var fans = new List<double>(); var memoryLoads = new List<double>(); var gpuLoads = new List<double>(); var gpuTemps = new List<double>();
                foreach (var hardware in (IEnumerable)_type.GetProperty("Hardware").GetValue(_computer, null)) ReadHardware(hardware, temps, loads, clocks, fans, memoryLoads, gpuLoads, gpuTemps);
                return new CpuMetrics { TemperatureC = temps.Count == 0 ? (double?)null : temps.Max(), CpuLoadPercent = loads.Count == 0 ? (double?)null : loads.Max(), AverageCoreClockMHz = clocks.Count == 0 ? (double?)null : clocks.Average(), MaximumCoreClockMHz = clocks.Count == 0 ? (double?)null : clocks.Max(), FanRpm = fans.Count == 0 ? (double?)null : fans.Max(), MemoryUsedPercent = memoryLoads.Count == 0 ? (double?)null : memoryLoads.Max(), GpuLoadPercent = gpuLoads.Count == 0 ? (double?)null : gpuLoads.Max(), GpuTemperatureC = gpuTemps.Count == 0 ? (double?)null : gpuTemps.Max() };
            }
            private static void ReadHardware(object hardware, List<double> temps, List<double> loads, List<double> clocks, List<double> fans, List<double> memoryLoads, List<double> gpuLoads, List<double> gpuTemps)
            {
                var type = hardware.GetType(); type.GetMethod("Update")?.Invoke(hardware, null); var hardwareType = Convert.ToString(type.GetProperty("HardwareType")?.GetValue(hardware, null)); var isGpu = hardwareType != null && hardwareType.IndexOf("Gpu", StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (var sensor in (IEnumerable)type.GetProperty("Sensors").GetValue(hardware, null))
                {
                    var sensorType = Convert.ToString(sensor.GetType().GetProperty("SensorType").GetValue(sensor, null)); var name = Convert.ToString(sensor.GetType().GetProperty("Name").GetValue(sensor, null)); var value = sensor.GetType().GetProperty("Value").GetValue(sensor, null); if (value == null) continue; var number = Convert.ToDouble(value);
                    if (hardwareType == "Cpu" && sensorType == "Temperature" && name.IndexOf("Distance", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("TjMax", StringComparison.OrdinalIgnoreCase) < 0 && number > 0 && number < 150) temps.Add(number);
                    else if (hardwareType == "Cpu" && sensorType == "Load" && (name == "CPU Total" || name == "CPU Core Max") && number >= 0 && number <= 100) loads.Add(number);
                    else if (hardwareType == "Cpu" && sensorType == "Clock" && name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 && number > 0) clocks.Add(number);
                    else if (isGpu && sensorType == "Load" && number >= 0 && number <= 100) gpuLoads.Add(number);
                    else if (isGpu && sensorType == "Temperature" && number > 0 && number < 150) gpuTemps.Add(number);
                    else if (hardwareType == "Memory" && sensorType == "Load" && number >= 0 && number <= 100) memoryLoads.Add(number);
                    else if (sensorType == "Fan" && number >= 0) fans.Add(number);
                }
                foreach (var child in (IEnumerable)type.GetProperty("SubHardware").GetValue(hardware, null)) ReadHardware(child, temps, loads, clocks, fans, memoryLoads, gpuLoads, gpuTemps);
            }
            public void Dispose() { try { _type.GetMethod("Close").Invoke(_computer, null); } catch { } }
        }
        private static double Median(IEnumerable<double> values) { var sorted = values.OrderBy(x => x).ToArray(); if (sorted.Length == 0) return 0; var middle = sorted.Length / 2; return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2.0 : sorted[middle]; }
        private sealed class SafetyAbortException : Exception { public SafetyAbortException(string message) : base(message) { } }
    }
}