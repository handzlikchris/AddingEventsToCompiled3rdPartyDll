using System.IO;

namespace EventILWeaver.Console.RevertToOriginal
{
    public class RevertToOriginalHandler: HandlerBase
    {
        public int Run(RevertToOriginalOptions options)
        {
            foreach (var targetPath in options.TargetDllPaths)
            {
                System.Console.WriteLine($"Processing... {targetPath}");

                RevertToBackup(targetPath);

                System.Console.WriteLine($"Processed! {targetPath}\r\n\r\n");
            }

            return 0;
        }

        private static bool RevertToBackup(string dllPath)
        {
            return ExecuteWithOptionalRetry(() =>
            {
                var backupPath = CreateBackupFilePath(dllPath);
                if (!File.Exists(backupPath))
                {
                    System.Console.WriteLine("Backup does not exist, unable to revert!");
                    return;
                }

                if (File.Exists(dllPath)) File.Delete(dllPath);

                File.Move(backupPath, dllPath);
                System.Console.WriteLine("Backup restored");
            });
        }
    }
}
