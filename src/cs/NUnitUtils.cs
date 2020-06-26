using System;
using System.Collections.Generic;
using System.IO;

namespace TestRunner
{
    public enum NUnitVersion
    {
        NUnitV2,
        NUnitV3
    }
    
    internal static class NUnitUtils
    {
        private const string Pattern = "NUnit.ConsoleRunner*";
        private const string ExecutableNameV2 = "/tools/nunit-console.exe";
        private const string ExecutableNameV3 = "/tools/nunit3-console.exe";

        internal static string NUnitRunnerPath { get; set; }
        internal static bool UseMono { get; set; }
        internal static string MonoExecutablePath { get; set; }

        internal static string FindRunner(NUnitVersion nUnitVersion = NUnitVersion.NUnitV3, string packagesPath = null)
        {
            return CommonUtils.FindRunner(Pattern, nUnitVersion == NUnitVersion.NUnitV2 ? ExecutableNameV2 : ExecutableNameV3, packagesPath);
        }

        internal static TestInfo GetNUnitTestInfo(RunMode runMode, string target,
            List<string> types = null, List<string> methods = null)
        {
            string arguments, modules = null, cons = target, executable;
            string tempFileName = System.IO.Path.GetTempFileName().Replace(".tmp", ".xml");
            List<TestCase> lst = new List<TestCase>();

            if (runMode == RunMode.Discover)
            {
                arguments = $"{target} --explore={tempFileName}";
            }
            else
            {
                if (types != null && types.Count > 0)
                {
                    modules = string.Join(",", types);
                    cons += "|" + string.Join("|", types);
                }

                if (methods != null && methods.Count > 0)
                {
                    modules = string.Join(",", methods);
                    cons += "|" + string.Join("|", methods);
                }

                if (modules != null)
                    modules = "--test=" + modules;
                arguments = $"{target}{" "}{modules} --inprocess --result={tempFileName}";
            }

            if (UseMono)
            {
                executable = MonoExecutablePath;
                arguments = $" --debug {NUnitRunnerPath} {arguments}";
            }
            else
            {
                executable = NUnitRunnerPath;
            }

            if (runMode != RunMode.Debug)
            {
                if (runMode == RunMode.Run)
                {
                    Console.WriteLine("nunit;{0}", cons);
                    Console.Out.Flush();
                }

                CommonUtils.RunTask(executable, arguments, target);
            }
            else
            {
                //debug method
                Console.WriteLine("nunit;{0};{1};{2};{3};{4};{5};{6}",
                    cons, NUnitRunnerPath, target, modules, "--inprocess", "--result", tempFileName);
                var s = Console.ReadLine();
            }

            if (File.Exists(tempFileName))
            {
                lst = CommonUtils.ReadDiscoveredTestsFromXml(tempFileName, runMode, lst);
                File.Delete(tempFileName);
            }

            if (lst.Count == 0)
                return null;
            lst.Sort();
            TestInfo t = new TestInfo {ModuleName = target, Type = "nunit", TestCases = lst};

            return t;
        }
    }
}