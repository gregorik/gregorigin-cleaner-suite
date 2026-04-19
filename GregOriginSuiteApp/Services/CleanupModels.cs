using System;
using System.Collections.Generic;
using System.Linq;

namespace GregOriginSuiteApp.Services
{
    public sealed class CleanupOptions
    {
        public bool WindowsTemp { get; init; }
        public bool UserTemp { get; init; }
        public bool EdgeCache { get; init; }
        public bool ChromeCache { get; init; }
        public bool Prefetch { get; init; }
        public bool SoftwareDistribution { get; init; }
        public bool BrowserSqlite { get; init; }
        public bool SystemRestore { get; init; }
        public bool RecycleBin { get; init; }
    }

    public sealed class CleanupTarget
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
    }

    public sealed class CleanupFileItem
    {
        public string TargetName { get; init; } = "";
        public string Path { get; init; } = "";
        public long Bytes { get; init; }
    }

    public sealed class CleanupDirectoryItem
    {
        public string TargetName { get; init; } = "";
        public string Path { get; init; } = "";
    }

    public sealed class CleanupSqliteAction
    {
        public string DatabasePath { get; init; } = "";
        public string TableName { get; init; } = "";
        public long Rows { get; init; }
        public string Failure { get; init; } = "";
    }

    public sealed class CleanupPlan
    {
        public DateTime CreatedAt { get; init; } = DateTime.Now;
        public List<CleanupTarget> Targets { get; } = new();
        public List<CleanupFileItem> Files { get; } = new();
        public List<CleanupDirectoryItem> Directories { get; } = new();
        public List<CleanupSqliteAction> SqliteActions { get; } = new();
        public List<string> SpecialActions { get; } = new();
        public List<string> Failures { get; } = new();
        public string AuditPath { get; set; } = "";
        public long TotalBytes => Files.Sum(f => f.Bytes);
    }

    public sealed class CleanupExecutionResult
    {
        public string AuditPath { get; init; } = "";
        public int FilesDeleted { get; set; }
        public int DirectoriesDeleted { get; set; }
        public long BytesDeleted { get; set; }
        public long SqliteRowsDeleted { get; set; }
        public List<string> Messages { get; } = new();
        public List<string> Failures { get; } = new();
        public bool Success => Failures.Count == 0;
    }
}
