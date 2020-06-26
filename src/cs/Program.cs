using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TestRunner
{
    public enum RunMode
    {
        Unknown = -1,
        Discover,
        Run,
        Debug
    }

    [Flags]
    public enum ModuleType
    {
        // Future enum values should be power of 2 as they represent flags
        Unknown = 0,
        NUnit = 1,
        XUnit = 2
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            int arg = 0, depth = 0;
            RunMode runMode = RunMode.Unknown;
            List<TestInfo> testInfos = new List<TestInfo>();
            List<string> target = new List<string>(),
                methods = new List<string>(),
                types = new List<string>();

            if (args.Length == 0)
            {
                DisplayUsage();
            }

            while (arg < args.Length)
            {
                switch (args[arg])
                {
                    case "discover":
                        if (runMode != RunMode.Unknown)
                        {
                            DisplayUsage();
                            return;
                        }

                        runMode = RunMode.Discover;
                        break;
                    case "run":
                        if (runMode != RunMode.Unknown)
                        {
                            DisplayUsage();
                            return;
                        }

                        runMode = RunMode.Run;
                        break;
                    case "debug":
                        if (runMode != RunMode.Unknown)
                        {
                            DisplayUsage();
                            return;
                        }
                        runMode = RunMode.Debug;
                        break;
                    case "-m":
                        NUnitUtils.UseMono = true;
                        NUnitUtils.MonoExecutablePath = args[++arg];
                        XUnitUtils.UseMono = NUnitUtils.UseMono;
                        NUnitUtils.MonoExecutablePath = NUnitUtils.MonoExecutablePath;
                        break;
                    case "-x":
                        XUnitUtils.XUnitRunnerPath = GetFullPath(args[++arg]);
                        break;
                    case "-n":
                        NUnitUtils.NUnitRunnerPath = GetFullPath(args[++arg]);
                        break;
                    case "-t":
                        target = args[++arg].Split(new[] {';'},
                            StringSplitOptions.RemoveEmptyEntries).ToList();
                        break;
                    case "-f":
                        target = new StreamReader(args[++arg]).ReadToEnd().Split(new[] {';'},
                            StringSplitOptions.RemoveEmptyEntries).ToList();
                        break;
                    case "-am":
                        methods = args[++arg].Split(';').ToList();
                        break;
                    case "-ac":
                        types = args[++arg].Split(';').ToList();
                        break;
                    case "-d":
                        depth = Convert.ToInt32(args[++arg]);
                        break;
                    default:
                        DisplayUsage();
                        return;
                }
                ++arg;
            }

            if (NUnitUtils.NUnitRunnerPath == "")
                NUnitUtils.NUnitRunnerPath = NUnitUtils.FindRunner();
            if (XUnitUtils.XUnitRunnerPath == "")
                XUnitUtils.XUnitRunnerPath = XUnitUtils.FindRunner();
            if (XUnitUtils.XUnitRunnerPath == null && NUnitUtils.NUnitRunnerPath == null)
            {
                Console.Error.WriteLine("Unit runners missing");
                return;
            }

            if (runMode == RunMode.Discover)
                testInfos = ProcessTests(runMode, target, testInfos);
            else
            {
                if (target.Count > 1 && (methods.Count != 0 || types.Count != 0))
                {
                    DisplayUsage();
                    return;
                }
                testInfos = ProcessTests(runMode, target, testInfos, types, methods);
            }

            int totalCount = 0, moduleCount = 0;

            foreach (var testInfo in testInfos)
            {
                var count = testInfo.TestCases.Count;

                if (count > 0)
                {
                    totalCount += count;
                    moduleCount++;
                    Console.Error.WriteLine(
                        runMode == 0
                            ? $"{count} tests found in {testInfo.ModuleName}"
                            : $"{count} tests run in {testInfo.ModuleName}");
                }
            }
            if (totalCount > 0)
            {
                Console.Error.WriteLine(runMode == 0
                    ? $"Total {totalCount} tests found in {moduleCount} assemblies"
                    : $"Total {totalCount} tests run in {moduleCount} assemblies");
            }

            return;
        }

        /// <summary>
        /// 
        /// </summary>
        private static void DisplayUsage()
        {
            Console.Error.WriteLine("testrun  -x <xunit location> -n <nunit location> " +
                                    "-m <mono location> " +
                                    " [discover [-t <semicolon sep target directory or assembly> | -f file] | " +
                                    "run [-t <semicolon sep target directory or assembly> | -f file] " +
                                    " -ac <semicolon sep classname> -am <semicolon sep methodname> | " +
                                    "debug  -t <target assembly> -am <methodname> ]");
            Environment.Exit(1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string GetFullPath(string path)
        {
            path = Environment.ExpandEnvironmentVariables(path);
            return Path.IsPathRooted(path) == false ? Path.GetFullPath(path) : path;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="testCasesTo"></param>
        /// <param name="testCasesFrom"></param>
        private static void AddSourceInfo(IReadOnlyList<TestCase> testCasesTo, IReadOnlyList<TestCase> testCasesFrom)
        {
            int toCount = testCasesTo.Count, fromCount = testCasesFrom.Count;
            int toIndex = 0, fromIndex = 0;

            while (toIndex < toCount && fromIndex < fromCount)
            {
                var testCaseTo = testCasesTo[toIndex];
                var testCaseFrom = testCasesFrom[fromIndex];
                var fromName = testCaseFrom.Name;
                var toName = testCaseTo.Name;
                
                toName = toName.Substring(0, toName.LastIndexOf('(') > 0 ? toName.LastIndexOf('(') : toName.Length);
                if (toName.Equals(fromName))
                {
                    testCaseTo.Source = testCaseFrom.Source;
                    testCaseTo.LineNumber = testCaseFrom.LineNumber;
                    toIndex++;
                }
                else if (String.Compare(toName, fromName, StringComparison.Ordinal) > 0)
                    fromIndex++;
                else
                    toIndex++;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="runMode"></param>
        /// <param name="targets"></param>
        /// <param name="testInfos"></param>
        /// <param name="types"></param>
        /// <param name="methods"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        private static List<TestInfo> ProcessTests(RunMode runMode, List<string> targets, List<TestInfo> testInfos,
            List<string> types = null, List<string> methods = null, int depth = 0)
        {
            foreach (string target in targets)
            {
                if (File.Exists(target))
                {
                    testInfos = ProcessTestsInFile(runMode, GetFullPath(target), testInfos, types, methods);
                }
                else if (Directory.Exists(target))
                {
                    var files = Directory.GetFiles(target, "*.dll");
                    
                    testInfos = ProcessTests(runMode, files.ToList(), testInfos, types, methods);
                    if (depth > 0)
                    {
                        var directories = Directory.GetDirectories(target);
                        testInfos = ProcessTests(runMode, directories.ToList(), testInfos, types, methods, --depth);
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error: In target name {0}", target);
                    return testInfos;
                }
            }
            return testInfos;
        }

        private static void DiscoverTests(TestInfo testInfo)
        {
            var testCases = MonoUtils.Monoc(testInfo.ModuleName, testInfo.TestCases);
            
            AddSourceInfo(testInfo.TestCases, testCases);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="runMode"></param>
        /// <param name="testInfos"></param>
        /// <param name="testInfo"></param>
        private static void ProcessTestInfo(RunMode runMode, List<TestInfo> testInfos, TestInfo testInfo)
        {
            if (testInfo != null)
            {
                testInfos.Add(testInfo);
                if (runMode == 0)
                {
                    DiscoverTests(testInfo);
                }
                testInfo.Print(runMode);
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="runMode"></param>
        /// <param name="target"></param>
        /// <param name="testInfos"></param>
        /// <param name="types"></param>
        /// <param name="methods"></param>
        /// <returns></returns>
        private static List<TestInfo> ProcessTestsInFile(RunMode runMode, string target, List<TestInfo> testInfos,
            List<string> types = null, List<string> methods = null)
        {
            ModuleType moduleType;

            if ((moduleType = GetModuleType(target)) == ModuleType.Unknown)
                return testInfos;
            // If there are NUnit tests
            if ((moduleType & ModuleType.NUnit) > 0)
            {
                ProcessTestInfo(runMode, testInfos, NUnitUtils.GetNUnitTestInfo(runMode, target, types, methods));
            }
            // If there are XUnit tests
            if ((moduleType & ModuleType.XUnit) > 0)
            {
                ProcessTestInfo(runMode, testInfos, XUnitUtils.GetXUnitTestInfo(runMode, target, types, methods));
            }
            return testInfos;
        }

        static ModuleType GetModuleType(string fileName)
        {
            AssemblyName[] assemblies;
            var moduleType = ModuleType.Unknown;
            
            try
            {
                assemblies = Assembly.LoadFile(fileName).GetReferencedAssemblies();
            }
            catch (Exception)
            {
                return ModuleType.Unknown;
            }
            if (assemblies.Length == 0)
                return 0;
            foreach (var assembly in assemblies)
            {
                if (assembly.ToString().ToLower().StartsWith("nunit"))
                    moduleType &= ModuleType.NUnit;
                else if (assembly.ToString().ToLower().StartsWith("xunit"))
                    moduleType &= ModuleType.XUnit;
            }

            return moduleType;
        }
    }
}