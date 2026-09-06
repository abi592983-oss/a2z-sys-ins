using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace A2ZSysIns
{
    [DataContract]
    public sealed class InspectionReport
    {
        [DataMember] public string SchemaVersion = "1.0";
        [DataMember] public string RuleSetVersion = "1.0";
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
}
