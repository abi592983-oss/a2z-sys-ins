using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace A2ZSysIns
{
    internal sealed class ProcessExecutionRequest
    {
        public string FileName;
        public string Arguments;
        public string WorkingDirectory;
        public int TimeoutMilliseconds = 25000;
        public Action<string, string> Output;
        public Action<int> Started;
    }

    internal sealed class ProcessExecutionResult
    {
        public DateTime StartedAt;
        public DateTime FinishedAt;
        public DateTime? LastOutputAt;
        public int? ProcessId;
        public int? ExitCode;
        public string StandardOutput;
        public string StandardError;
        public bool TimedOut;
        public bool Cancelled;
        public bool Started;
    }

    internal static class ProcessExecutionService
    {
        public static async Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellation)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FileName)) throw new ArgumentException("A process executable is required.", "request");
            cancellation.ThrowIfCancellationRequested();
            var result = new ProcessExecutionResult { StartedAt = DateTime.Now };
            var stdout = new StringBuilder(); var stderr = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(request.FileName, request.Arguments ?? "")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true, WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory) ? Environment.CurrentDirectory : request.WorkingDirectory
                };
                DataReceivedEventHandler receiveOut = (sender, e) => Add("stdout", e.Data, stdout);
                DataReceivedEventHandler receiveErr = (sender, e) => Add("stderr", e.Data, stderr);
                void Add(string stream, string line, StringBuilder destination)
                {
                    if (line == null) return;
                    lock (destination) destination.AppendLine(line);
                    result.LastOutputAt = DateTime.Now;
                    request.Output?.Invoke(stream, line);
                }
                process.OutputDataReceived += receiveOut; process.ErrorDataReceived += receiveErr;
                if (!process.Start()) throw new InvalidOperationException("Process did not start.");
                result.Started = true; result.ProcessId = process.Id; request.Started?.Invoke(process.Id);
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1, request.TimeoutMilliseconds));
                try
                {
                    while (!process.HasExited)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (DateTime.UtcNow >= deadline) { result.TimedOut = true; break; }
                        await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    result.Cancelled = true;
                    throw;
                }
                finally
                {
                    if (!process.HasExited && (result.TimedOut || result.Cancelled || cancellation.IsCancellationRequested))
                    {
                        try { process.Kill(); } catch { }
                    }
                    if (!process.HasExited) process.WaitForExit(3000);
                    if (process.HasExited) result.ExitCode = process.ExitCode;
                    result.FinishedAt = DateTime.Now; result.StandardOutput = stdout.ToString(); result.StandardError = stderr.ToString();
                    process.OutputDataReceived -= receiveOut; process.ErrorDataReceived -= receiveErr;
                }
            }
            if (result.TimedOut) throw new TimeoutException(request.FileName + " timed out after " + request.TimeoutMilliseconds / 1000 + " seconds.");
            return result;
        }
    }
}
