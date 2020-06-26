using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace TestRunner
{
    internal static class XUnitUtils
    {
        private const string Pattern = "xunit.runner.console*";
        private const string ExecutableName = "/tools/xunit.console.exe";

        internal static string FindRunner(string packagesPath = null)
        {
            return CommonUtils.FindRunner(Pattern, ExecutableName, packagesPath);
        }

        internal static string XUnitRunnerPath { get; set; }
        internal static bool UseMono { get; set; }
        internal static string MonoExecutablePath { get; set; }

        internal static TestInfo GetXUnitTestInfo(RunMode runMode, string target,
            List<string> types = null, List<string> methods = null)
        {
            List<TestCase> testCases = new List<TestCase>();
            string arguments = null, cons = target;
            string executable, executableArguments;
            string tempFile = System.IO.Path.GetTempFileName().Replace(".tmp", ".xml");

            if (types != null && types.Count > 0)
            {
                arguments = " -class " + string.Join(" -class ", types.ToArray());
                cons += $"|{string.Join("|", types.ToArray())}";
            }

            if (methods != null && methods.Count > 0)
            {
                // XUnit doesn't run inline data functions individually?
                arguments = methods.Select(
                        method => method.Substring(0, method.IndexOf('(') == -1 ? method.Length : method.IndexOf('(')))
                    .Aggregate(arguments, (current, s) =>
                        $"{current} -method {s}");
                cons += $"|{string.Join("|", methods.ToArray())}";
            }

            if (UseMono)
            {
                executable = MonoExecutablePath;
                executableArguments = $" --debug {XUnitRunnerPath} {target}{arguments} -xml {tempFile}";
            }
            else
            {
                executable = XUnitRunnerPath;
                executableArguments = $"{target}{arguments} -xml {tempFile}";
            }

            switch (runMode)
            {
                case RunMode.Unknown:
                    break;
                case RunMode.Discover:
                    //discover tests
                    testCases = XunitDiscover(target, testCases);
                    break;
                case RunMode.Run:
                    //run tests
                    Console.WriteLine("xunit;{0}", cons);
                    Console.Out.Flush();
                    CommonUtils.RunTask(executable, executableArguments, target);
                    break;
                case RunMode.Debug:
                {
                    //debug method
                    var m = methods[0].Substring(0,
                        methods[0].IndexOf('(') == -1 ? methods[0].Length : methods[0].IndexOf('('));
                    Console.WriteLine("xunit;{0};{1};{2};{3};{4};{5};{6}",
                        cons, XUnitRunnerPath, target, "-method", m, "-xml", tempFile);
                    Console.ReadLine();
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(runMode), runMode, null);
            }

            if (runMode != RunMode.Discover && File.Exists(tempFile))
            {
                testCases = CommonUtils.ReadDiscoveredTestsFromXml(tempFile, runMode, testCases);
                File.Delete(tempFile);
            }

            if (testCases.Count == 0)
                return null;
            testCases.Sort();

            var testInfo = new TestInfo {ModuleName = target, Type = "xunit", TestCases = testCases};

            return testInfo;
        }

        static List<TestCase> XunitDiscover(string fileName, List<TestCase> testCases)
        {
            if (fileName == null)
                return testCases;

            var currentDirectory = Directory.GetCurrentDirectory();
            var directory = Path.GetDirectoryName(fileName) ?? currentDirectory;

            Directory.SetCurrentDirectory(directory);
            AppDomain.CurrentDomain.AssemblyResolve += CommonUtils.AssemblyResolve;

            var assembly = new XunitProjectAssembly {AssemblyFilename = fileName};

            assembly.Configuration.AppDomain = AppDomainSupport.Denied;
            assembly.Configuration.PreEnumerateTheories = true;

            var options = TestFrameworkOptions.ForDiscovery(assembly.Configuration);
            var domain = assembly.Configuration.AppDomainOrDefault;

            try
            {
                using (var controller = new XunitFrontController(domain,
                    assembly.AssemblyFilename))
                {
                    using (var sink = new TestDiscoverySink())
                    {
                        controller.Find(false, sink, options);
                        sink.Finished.WaitOne();

                        var count = sink.TestCases.Count;

                        foreach (var t in sink.TestCases)
                        {
                            var className = t.TestMethod.TestClass.Class.Name;
                            var methodName = t.TestMethod.Method.Name;
                            var displayName = t.DisplayName.Contains(className)
                                ? t.DisplayName
                                : $"{className}.{t.DisplayName}";
                            var testCase = new TestCase($"{className}.{methodName}", displayName);

                            testCases.Add(testCase);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("{0} {1} {2}", fileName, e.ToString(), e.Message);
            }

            AppDomain.CurrentDomain.AssemblyResolve -= CommonUtils.AssemblyResolve;
            Directory.SetCurrentDirectory(currentDirectory);
            return testCases;
        }
    }
}