// Copyright (c) Xenko contributors (https://xenko.com)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Xenko.Core.Assets
{
    class NuGetAssemblyResolver
    {
        static bool assembliesResolved;
        static object assembliesLock = new object();
        static List<string> assemblies;

        internal static void DisableAssemblyResolve()
        {
            assembliesResolved = true;
        }

        [ModuleInitializer(-100000)]
        internal static void __Initialize__()
        {
            // Only perform this for entry assembly
            if (!(Assembly.GetEntryAssembly() == null // .NET FW: null during module .ctor
                || Assembly.GetEntryAssembly() == Assembly.GetCallingAssembly())) // .NET Core: check against calling assembly
                return;

            // delete old temp files
            var dirs = Directory.GetDirectories(Path.GetTempPath(), "Xenko*");
            if (dirs != null)
            {
                foreach (string s in dirs)
                    Directory.Delete(s, true);
            }

            // Make sure our nuget local store is added to nuget config
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string xenkoFolder = null;
            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder, @"build\Xenko.sln")))
                {
                    xenkoFolder = folder;
                    var settings = NuGet.Configuration.Settings.LoadDefaultSettings(null);
                    // Remove non-existing sources: https://github.com/xenko3d/xenko/issues/338
                    RemoveDeletedSources(settings, "Xenko");
                    CheckPackageSource(settings, $"Xenko Dev {xenkoFolder}", Path.Combine(xenkoFolder, @"bin\packages"));
                    settings.SaveToDisk();
                    break;
                }
                folder = Path.GetDirectoryName(folder);
            }

            // Note: we perform nuget restore inside the assembly resolver rather than top level module ctor (otherwise it freezes)
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                if (!assembliesResolved)
                {
                    lock (assembliesLock)
                    {
                        // Note: using NuGet will try to recursively resolve NuGet.*.resources.dll, so set assembliesResolved right away so that it bypasses everything
                        assembliesResolved = true;
                        CancellationTokenSource s_cts = new CancellationTokenSource();
                        var logger = new Logger();
                        try
                        {
                            s_cts.CancelAfter(5000);
                            var (request, result) = RestoreHelper.Restore(logger, Assembly.GetExecutingAssembly().GetName().Name, new VersionRange(new NuGetVersion(XenkoVersion.NuGetVersion))).Result;
                            assemblies = RestoreHelper.ListAssemblies(request, result);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }

                if (assemblies != null)
                {
                    var aname = new AssemblyName(eventArgs.Name);
                    if (aname.Name.StartsWith("Microsoft.Build") && aname.Name != "Microsoft.Build.Locator")
                        return null;
                    var assemblyPath = assemblies.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == aname.Name);
                    if (assemblyPath != null)
                    {
                        return Assembly.LoadFrom(assemblyPath);
                    }
                }
                return null;
            };
        }

        private static void RemoveDeletedSources(ISettings settings, string prefixName)
        {
            var packageSources = settings.GetSection("packageSources");
            if (packageSources != null)
            {
                foreach (var packageSource in packageSources.Items.OfType<SourceItem>().ToList())
                {
                    var path = packageSource.GetValueAsPath();

                    if (packageSource.Key.StartsWith(prefixName)
                        && Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile // make sure it's a valid file URI
                        && !Directory.Exists(path)) // detect if directory has been deleted
                    {
                        // Remove entry from packageSources
                        settings.Remove("packageSources", packageSource);
                    }
                }
            }
        }

        private static void CheckPackageSource(ISettings settings, string name, string url)
        {
            settings.AddOrUpdate("packageSources", new SourceItem(name, url));
        }

        public class Logger : ILogger
        {
            private object logLock = new object();
            public List<(LogLevel Level, string Message)> Logs { get; } = new List<(LogLevel, string)>();

            public void LogDebug(string data)
            {
                Log(LogLevel.Debug, data);
            }

            public void LogVerbose(string data)
            {
                Log(LogLevel.Verbose, data);
            }

            public void LogInformation(string data)
            {
                Log(LogLevel.Information, data);
            }

            public void LogMinimal(string data)
            {
                Log(LogLevel.Minimal, data);
            }

            public void LogWarning(string data)
            {
                Log(LogLevel.Warning, data);
            }

            public void LogError(string data)
            {
                Log(LogLevel.Error, data);
            }

            public void LogInformationSummary(string data)
            {
                Log(LogLevel.Information, data);
            }

            public void LogErrorSummary(string data)
            {
                Log(LogLevel.Error, data);
            }

            public void Log(LogLevel level, string data)
            {
                lock (logLock)
                {
                    Debug.WriteLine($"[{level}] {data}");
                    Logs.Add((level, data));
                }
            }

            public Task LogAsync(LogLevel level, string data)
            {
                Log(level, data);
                return Task.CompletedTask;
            }

            public void Log(ILogMessage message)
            {
                Log(message.Level, message.Message);
            }

            public Task LogAsync(ILogMessage message)
            {
                Log(message);
                return Task.CompletedTask;
            }
        }
    }
}
