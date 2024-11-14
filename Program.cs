using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DiskCleanerApp
{
    class Program
    {
        private static Dictionary<string, string> cleanupPaths;
        private static long totalFreedSpace = 0;
        private static int successCount = 0;
        private static int failCount = 0;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (!IsAdministrator())
            {
                RestartAsAdmin();
                return;
            }

            CheckDotNetVersion();
            SetupCleanupPaths();
            Console.WriteLine("正在清理...");

            foreach (var item in cleanupPaths)
            {
                Console.WriteLine($"正在清理 {item.Key}...");
                CleanDirectory(item.Value);
            }

            Console.WriteLine("\n清理完成！");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static void CheckDotNetVersion()
        {
            int releaseKey = GetDotNetReleaseKey();
            if (releaseKey < 394802) // .NET 4.6.2 及以上
            {
                Console.WriteLine("检测到 .NET 版本过低，正在自动安装 .NET Framework...");
                Process.Start("https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net462-web-installer");
                Environment.Exit(0);
            }
            Console.WriteLine(".NET 版本已满足要求。");
        }

        static int GetDotNetReleaseKey()
        {
            using (var regKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
            {
                return (int)(regKey?.GetValue("Release") ?? 0);
            }
        }

        static void SetupCleanupPaths()
        {
            string osVersion = GetOSVersion();
            cleanupPaths = osVersion switch
            {
                string version when version.Contains("Windows 7") => new Dictionary<string, string>
        {
            { "系统临时文件夹", Path.GetTempPath() },
            { "用户临时文件夹", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\Local\\Temp") },
            { "Windows 更新缓存", @"C:\Windows\SoftwareDistribution\Download" },
            { "预读文件", @"C:\Windows\Prefetch" }
        },
                string version when version.Contains("Windows 10") || version.Contains("Windows 11") => new Dictionary<string, string>
        {
            { "系统临时文件夹", Path.GetTempPath() },
            { "用户临时文件夹", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\Local\\Temp") },
            { "Windows 更新缓存", @"C:\Windows\SoftwareDistribution\Download" },
            { "预读文件", @"C:\Windows\Prefetch" },
            { "错误报告文件", @"C:\ProgramData\Microsoft\Windows\WER" },
            { "Edge 浏览器缓存", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\Local\\Microsoft\\Edge\\User Data\\Default\\Cache") }
        },
                _ => throw new NotSupportedException("不支持的操作系统版本")
            };

            Console.WriteLine($"已检测到操作系统版本: {osVersion}");
        }


        static string GetOSVersion()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
            {
                foreach (var os in searcher.Get())
                {
                    return os["Caption"]?.ToString() ?? "未知操作系统";
                }
            }
            return "未知操作系统";
        }

        static bool IsAdministrator()
        {
            //using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            //{
            //    WindowsPrincipal principal = new WindowsPrincipal(identity);
            //    return principal.IsInRole(WindowsBuiltInRole.Administrator);
            //}
            return true;
        }

        static void RestartAsAdmin()
        {
            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            ProcessStartInfo startInfo = new ProcessStartInfo(exeName)
            {
                Verb = "runas"
            };
            try
            {
                Process.Start(startInfo);
            }
            catch
            {
                Console.WriteLine("未能以管理员身份重新启动程序。");
            }
            Environment.Exit(0);
        }

        static void CleanDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    foreach (var file in Directory.GetFiles(directoryPath))
                    {
                        try
                        {
                            long fileSize = new FileInfo(file).Length;
                            File.Delete(file);
                            successCount++;
                            totalFreedSpace += fileSize;
                        }
                        catch
                        {
                            failCount++;
                        }

                        UpdateStatistics();
                    }

                    foreach (var dir in Directory.GetDirectories(directoryPath))
                    {
                        CleanDirectory(dir);
                        try
                        {
                            Directory.Delete(dir);
                            successCount++;
                        }
                        catch
                        {
                            failCount++;
                        }

                        UpdateStatistics();
                    }
                }
            }
            catch
            {
                failCount++;
                UpdateStatistics();
            }
        }

        static void UpdateStatistics()
        {
            int currentCursorTop = Console.CursorTop;

            Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 13);

            // 设置成功删除信息为绿色
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"成功删除: {successCount} 个文件/目录".PadRight(Console.WindowWidth));

            // 设置删除失败信息为红色
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"删除失败: {failCount} 个文件/目录".PadRight(Console.WindowWidth));

            // 设置释放空间信息为黄色
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"释放空间: {totalFreedSpace / (1024 * 1024):F2} MB".PadRight(Console.WindowWidth));

            // 重置颜色
            Console.ResetColor();
            Console.SetCursorPosition(0, currentCursorTop);
        }
    }
}
