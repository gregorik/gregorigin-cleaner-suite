using Microsoft.Win32;

namespace GregOriginSuiteApp.Models
{
    public class StartupApp
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Location { get; set; } = "";
        public string State { get; set; } = "";
        public string Key { get; set; } = "";
        public string EntryType { get; set; } = "";
        public string RegistryHive { get; set; } = "";
        public string RegistryPath { get; set; } = "";
        public RegistryValueKind RegistryValueKind { get; set; } = RegistryValueKind.String;
        public bool IsRegistry => !string.IsNullOrWhiteSpace(RegistryHive);
        public bool IsScheduledTask => EntryType == "ScheduledTask";
    }
}
