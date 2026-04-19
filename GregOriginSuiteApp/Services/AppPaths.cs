using System;
using System.IO;

namespace GregOriginSuiteApp.Services
{
    public static class AppPaths
    {
        public static string AppDataDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "GregOriginSuite");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string AuditDirectory
        {
            get
            {
                string dir = Path.Combine(AppDataDirectory, "Audit");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string BackupDirectory
        {
            get
            {
                string dir = Path.Combine(AppDataDirectory, "Backups");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string StartupBackupPath => Path.Combine(BackupDirectory, "startup-backups.json");
        public static string ServiceBackupPath => Path.Combine(BackupDirectory, "service-backups.json");
    }
}
