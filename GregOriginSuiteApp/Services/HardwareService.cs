using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using GregOriginSuiteApp.Models;

namespace GregOriginSuiteApp.Services
{
    public sealed class HardwareSnapshot
    {
        public string Cpu { get; init; } = "Unknown";
        public string Ram { get; init; } = "Unknown";
        public string Gpu { get; init; } = "Unknown";
        public string Os { get; init; } = "Unknown";
        public IReadOnlyList<SmartDrive> Drives { get; init; } = Array.Empty<SmartDrive>();
    }

    public sealed class LargeFileScanResult
    {
        public List<string> Files { get; } = new();
        public bool Cancelled { get; init; }
    }

    public sealed class HardwareService
    {
        public Task<HardwareSnapshot> LoadSnapshotAsync()
        {
            return Task.Run(() =>
            {
                string cpu = ReadFirstWmiValue("SELECT Name FROM Win32_Processor", "Name");
                string gpu = ReadFirstWmiValue("SELECT Name FROM Win32_VideoController", "Name");
                string os = ReadFirstWmiValue("SELECT Caption FROM Win32_OperatingSystem", "Caption");
                string ram = "Unknown";

                using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (ulong.TryParse(obj["TotalVisibleMemorySize"]?.ToString(), out ulong kb))
                        {
                            ram = Math.Round(kb / 1048576.0, 1) + " GB";
                        }
                    }
                }

                return new HardwareSnapshot
                {
                    Cpu = cpu,
                    Ram = ram,
                    Gpu = gpu,
                    Os = os,
                    Drives = LoadDriveStatuses()
                };
            });
        }

        public Task<LargeFileScanResult> ScanLargeFilesAsync(string rootPath, int topCount, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var top = new List<FileInfo>();
                var result = new LargeFileScanResult();
                var options = new System.IO.EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    ReturnSpecialDirectories = false
                };

                try
                {
                    foreach (var file in new DirectoryInfo(rootPath).EnumerateFiles("*", options))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return new LargeFileScanResult
                            {
                                Cancelled = true
                            };
                        }

                        AddToTop(top, file, topCount);
                    }
                }
                catch
                {
                }

                foreach (var file in top.OrderByDescending(f => SafeLength(f)))
                {
                    result.Files.Add($"{Math.Round(SafeLength(file) / 1048576.0, 2)} MB - {file.FullName}");
                }

                return result;
            }, cancellationToken);
        }

        private static void AddToTop(List<FileInfo> top, FileInfo file, int topCount)
        {
            long length = SafeLength(file);
            if (top.Count < topCount)
            {
                top.Add(file);
                return;
            }

            int smallestIndex = 0;
            long smallest = SafeLength(top[0]);
            for (int i = 1; i < top.Count; i++)
            {
                long candidate = SafeLength(top[i]);
                if (candidate < smallest)
                {
                    smallest = candidate;
                    smallestIndex = i;
                }
            }

            if (length > smallest)
            {
                top[smallestIndex] = file;
            }
        }

        private static long SafeLength(FileInfo file)
        {
            try
            {
                return file.Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string ReadFirstWmiValue(string query, string property)
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject obj in searcher.Get())
            {
                string value = obj[property]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "Unknown";
        }

        private static IReadOnlyList<SmartDrive> LoadDriveStatuses()
        {
            var drives = new List<SmartDrive>();
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, Model, Status FROM Win32_DiskDrive");
            foreach (ManagementObject obj in searcher.Get())
            {
                drives.Add(new SmartDrive
                {
                    Drive = obj["DeviceID"]?.ToString() ?? "",
                    Model = obj["Model"]?.ToString() ?? "",
                    Status = obj["Status"]?.ToString() ?? ""
                });
            }

            return drives;
        }
    }
}
