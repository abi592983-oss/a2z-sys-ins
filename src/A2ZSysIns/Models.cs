using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace A2ZSysIns
{
    [DataContract]
    public sealed class InspectionReport
    {
        [DataMember] public string SchemaVersion = "2.0";
        [DataMember] public string RuleSetVersion = "2.0-evidence-only";
        [DataMember] public List<Measurement> Measurements = new List<Measurement>();
        [DataMember] public List<LogEntry> DiagnosticLog = new List<LogEntry>();
        [DataMember] public List<VolumeRecord> Volumes = new List<VolumeRecord>();
        [DataMember] public double? MemoryUsedPercent;
        [DataMember] public bool IsAdministrator;
        [DataMember] public string InspectionId;
        [DataMember] public DateTime StartedAt;
        [DataMember] public DateTime CompletedAt;
        [DataMember] public string CustomerReference;
        [DataMember] public string JobNumber;
        [DataMember] public string Technician;
        [DataMember] public string ReportedProblem;
        [DataMember] public string TechnicianNotes;
        [DataMember] public Dictionary<string, string> System = new Dictionary<string, string>();
        [DataMember] public List<DriveInfoRecord> Drives = new List<DriveInfoRecord>();
        [DataMember] public List<SensorRecord> Sensors = new List<SensorRecord>();
        [DataMember] public List<EventFinding> Events = new List<EventFinding>();
        [DataMember] public List<Finding> Findings = new List<Finding>();
        [DataMember] public List<CategoryScore> Scores = new List<CategoryScore>();
        [DataMember] public int? OverallScore;
        [DataMember] public string OverallStatus;
        [DataMember] public List<string> Limitations = new List<string>();
    }

    [DataContract] public sealed class DriveInfoRecord
    {
        [DataMember] public string Model;
        [DataMember] public string Serial;
        [DataMember] public string Interface;
        [DataMember] public long SizeBytes;
        [DataMember] public string SmartStatus;
        [DataMember] public string RawEvidence;
        [DataMember] public string DeviceId;
        [DataMember] public string PnpId;
        [DataMember] public bool? SmartPassed;
        [DataMember] public double? RemainingLifePercent;
        [DataMember] public string LifeMeaning = "Not measured; no validated life attribute";
        [DataMember] public string Assessment = "Not assessed";
        [DataMember] public Dictionary<string, long> Attributes = new Dictionary<string, long>();
    }
    [DataContract] public sealed class SensorRecord
    {
        [DataMember] public string Hardware;
        [DataMember] public string Name;
        [DataMember] public string Type;
        [DataMember] public float? Current;
        [DataMember] public float? Minimum;
        [DataMember] public float? Maximum;
        [DataMember] public string Unit;
    }
    [DataContract] public sealed class EventFinding
    {
        [DataMember] public string Source;
        [DataMember] public int EventId;
        [DataMember] public string Level;
        [DataMember] public int Count;
        [DataMember] public DateTime? Latest;
        [DataMember] public string Summary;
        [DataMember] public string Signature;
        [DataMember] public string Cause;
        [DataMember] public List<string> RawEvents = new List<string>();
    }
    [DataContract] public sealed class Finding
    {
        [DataMember] public string Severity;
        [DataMember] public string Category;
        [DataMember] public string Title;
        [DataMember] public string Explanation;
        [DataMember] public string Recommendation;
        [DataMember] public string Evidence;
    }
    [DataContract] public sealed class CategoryScore
    {
        [DataMember] public string Category { get; set; }
        [DataMember] public int? Score { get; set; }
        [DataMember] public string Status { get; set; }
        [DataMember] public string Reason { get; set; }
    }
    [DataContract] public sealed class Measurement
    {
        [DataMember] public string Target;
        [DataMember] public string Source;
        [DataMember] public string Status;
        [DataMember] public string Reason;
        [DataMember] public string Response;
    }
    [DataContract] public sealed class LogEntry
    {
        [DataMember] public DateTime AtUtc;
        [DataMember] public string Action;
        [DataMember] public string Response;
    }
    [DataContract] public sealed class VolumeRecord
    {
        [DataMember] public string Name;
        [DataMember] public long TotalBytes;
        [DataMember] public long FreeBytes;
    }
}
