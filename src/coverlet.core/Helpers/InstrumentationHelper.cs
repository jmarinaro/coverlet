using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Microsoft.Extensions.FileSystemGlobbing;

using Coverlet.Core.Instrumentation;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Coverlet.Core.Helpers
{
    public static class InstrumentationHelper
    {
        public static string[] GetDependencies(string module)
        {
            IEnumerable<string> modules = Directory.GetFiles(Path.GetDirectoryName(module), "*.dll");
            modules = modules.Where(a => Path.GetFileName(a) != Path.GetFileName(module));
            return modules.ToArray();
        }

        public static bool HasPdb(string module)
        {
            using (var moduleStream = File.OpenRead(module))
            using (var peReader = new PEReader(moduleStream))
            {
                foreach (var entry in peReader.ReadDebugDirectory())
                {
                    if (entry.Type == DebugDirectoryEntryType.CodeView)
                    {
                        var codeViewData = peReader.ReadCodeViewDebugDirectoryData(entry);
                        var peDirectory = Path.GetDirectoryName(module);
                        return File.Exists(Path.Combine(peDirectory, Path.GetFileName(codeViewData.Path)));
                    }
                }

                return false;
            }
        }

        public static void CopyCoverletDependency(string module)
        {
            var directory = Path.GetDirectoryName(module);
            if (Path.GetFileNameWithoutExtension(module) == "coverlet.core")
                return;

            var assembly = typeof(Coverage).Assembly;
            string name = Path.GetFileName(assembly.Location);
            File.Copy(assembly.Location, Path.Combine(directory, name), true);
        }

        public static void BackupOriginalModule(string module, string identifier)
        {
            var backupPath = Path.Combine(
                Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(module) + "_" + identifier + ".dll"
            );

            File.Copy(module, backupPath);
        }

        public static void RestoreOriginalModule(string module, string identifier)
        {
            var backupPath = Path.Combine(
                Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(module) + "_" + identifier + ".dll"
            );

            // Restore the original module - retry up to 10 times, since the destination file could be locked
            // See: https://github.com/tonerdo/coverlet/issues/25
            var currentSleep = 6;
            Func<TimeSpan> retryStrategy = () => {
                var sleep = TimeSpan.FromMilliseconds(currentSleep);
                currentSleep *= 2;
                return sleep;
            };

            RetryHelper.Retry(() => {
                File.Copy(backupPath, module, true);
                File.Delete(backupPath);
            }, retryStrategy, 10);
        }

        public static IEnumerable<string> ReadHitsFile(string path)
        {
            // Retry hitting the hits file - retry up to 10 times, since the file could be locked
            // See: https://github.com/tonerdo/coverlet/issues/25
            var currentSleep = 6;
            Func<TimeSpan> retryStrategy = () =>
            {
                var sleep = TimeSpan.FromMilliseconds(currentSleep);
                currentSleep *= 2;
                return sleep;
            };

            return RetryHelper.Do(() => File.ReadLines(path), retryStrategy, 10);
        }

        public static void DeleteHitsFile(string path)
        {
            // Retry hitting the hits file - retry up to 10 times, since the file could be locked
            // See: https://github.com/tonerdo/coverlet/issues/25
            var currentSleep = 6;
            Func<TimeSpan> retryStrategy = () => {
                var sleep = TimeSpan.FromMilliseconds(currentSleep);
                currentSleep *= 2;
                return sleep;
            };

            RetryHelper.Retry(() => File.Delete(path), retryStrategy, 10);
        }
        
        public static string[] GetExcludedFiles(string[] exclusionRules, string parentDir) {
            if (exclusionRules == null || exclusionRules.Length == 0 ) return null;
            var matcher = new Matcher();
            foreach (var exclusionRule in exclusionRules)
            {
                matcher.AddInclude(exclusionRule);
            }
        
            DirectoryInfo directoryInfo = new DirectoryInfo(parentDir);
            
            var fileMatchResult = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));
            return fileMatchResult.Files
                .Select(
                    f => System.IO.Path.GetFullPath(
                        System.IO.Path.Combine(directoryInfo.ToString(), f.Path)
                    )
                )
                .ToArray();
        }
    }
}

