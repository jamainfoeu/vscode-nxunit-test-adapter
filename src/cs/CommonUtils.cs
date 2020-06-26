using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace TestRunner
{
    internal static class CommonUtils
    {
        internal static string FindRunner(string pattern, string executableName, string packagesPath = null)
        {
            var resolvedPath = packagesPath ?? $"{Directory.GetCurrentDirectory()}/packages";

            if (!Directory.Exists(resolvedPath))
                return null;

            var directories = Directory.GetDirectories(resolvedPath, pattern);

            if (directories.Length == 0) return null;

            var executablePath = $"{directories[0]}{executableName}";
            
            return File.Exists(executablePath) ? executablePath : null;
        }

        internal static List<TestCase> ReadDiscoveredTestsFromXml(string xml, RunMode runMode, List<TestCase> lst)
        {
            using (XmlReader reader = XmlReader.Create(xml))
            {
                while (reader.Read())
                {
                    // Only detect start elements.
                    TestCase testCase = null;
                    if (reader.IsStartElement())
                    {
                        if (reader.Name == "test-case")
                        {
                            // Nunit
                            string fullName = reader["fullname"] ?? Regex.Unescape(reader["name"]);
                            var methodName = reader["methodname"];
                            var className = reader["classname"];

                            if (methodName != null && className != null)
                                testCase = new TestCase($"{className}.{methodName}", fullName);
                            else
                                testCase = new TestCase(fullName, fullName);

                            lst.Add(testCase);
                        }
                        else if (reader.Name == "test")
                        {
                            // Xunit
                            var fullName = reader["name"];
                            var methodName = reader["method"];
                            var className = reader["type"];

                            if (fullName == null && methodName == null && className == null)
                                continue;
                            fullName = Regex.Unescape(fullName);
                            if (!fullName.Contains(className))
                                fullName = $"{className}.{fullName}";

                            if (methodName != null && className != null)
                                testCase = new TestCase($"{className}.{methodName}", fullName);
                            else
                                testCase = new TestCase(fullName, fullName);
                            lst.Add(testCase);
                        }

                        if (runMode != RunMode.Discover && testCase != null)
                        {
                            testCase.Result = reader["result"];
                            testCase.Time = reader["time"];
                            //xunit returns Failure
                            if (testCase.Result == "Failure" || testCase.Result == "Fail")
                                testCase.Result = "Failed";
                            if (testCase.Time == null)
                                testCase.Time = reader["duration"];
                            if (testCase.Result == "Failed")
                            {
                                if (reader.ReadToDescendant("message"))
                                    testCase.Message = reader.ReadElementContentAsString();
                                if (reader.ReadToNextSibling("stack-trace"))
                                    testCase.Stack = reader.ReadElementContentAsString();

                                var debugInfo = GetDebugInfo(testCase.Stack);

                                if (debugInfo != null)
                                {
                                    testCase.Source = debugInfo.Item1;
                                    testCase.LineNumber = debugInfo.Item2.ToString();
                                }
                            }

                            testCase = null;
                        }
                    }
                }
            }

            return lst;
        }

        private static Tuple<string, int> GetDebugInfo(string stackTrace)
        {
            const string begin = " in ";
            const string separator = ":";

            if (stackTrace == null)
                return null;

            var st = stackTrace.IndexOf(begin, 0, StringComparison.Ordinal);

            if (st == -1)
                return null;

            var et = stackTrace.LastIndexOf(separator, StringComparison.Ordinal);

            if (et == -1)
                return null;
            st += begin.Length;

            var s = stackTrace.Substring(st, et - st);

            char[] charsToTrim = {' ', '\n', '\r'};

            s = s.Trim(charsToTrim);
            if (File.Exists(s) == false)
                return null;

            var li = stackTrace.Substring(et + 1, stackTrace.Length - (et + 1));
            var no = Regex.Match(li, @"\d+").Value;

            int.TryParse(no, out var n);
            return new Tuple<string, int>(s, n - 1);
        }

        internal static Tuple<string, int> GetDebugInfo(Mono.Cecil.Cil.MethodBody methodBody)
        {
            var fileName = string.Empty;
            var line = -1;

            if (methodBody == null)
                return null;

            int instructionIndex = 0;

            while (instructionIndex < methodBody.Instructions.Count)
            {
                Mono.Cecil.Cil.SequencePoint sequencePoint;
                
                if ((sequencePoint = methodBody.Method.DebugInformation.GetSequencePoint(
                        methodBody.Instructions[instructionIndex++]
                    )) == null || sequencePoint.IsHidden)
                    continue;
                fileName = sequencePoint.Document.Url;
                line = sequencePoint.StartLine;
                break;
            }

            return new Tuple<string, int>(fileName, line);
        }

        internal static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string file = Path.Combine(currentDirectory, args.Name.Split(',')[0]);
            string[] extensions = {".dll", ".exe"};

            foreach (var ext in extensions)
            {
                var assemblyFile = $"{file}{ext}";

                if (!File.Exists(assemblyFile)) continue;
                try
                {
                    return Assembly.LoadFrom(assemblyFile);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("Error Resolving {0}, {1}", assemblyFile, exception.ToString());
                }
            }

            return null;
        }

        internal static void RunTask(string exe, string args, string f)
        {
            Task<string> output = null, error = null;
            var process = new Process
            {
                StartInfo =
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            output = Task.Run(() => process.StandardOutput.ReadToEndAsync());
            error = Task.Run(() => process.StandardError.ReadToEndAsync());
            process.WaitForExit();
            if (error.Result.Length > 0 || output.Result.Contains("Exception"))
            {
                Console.Error.WriteLine("Error while running {0}", f);
                Console.Error.WriteLine(output.Result);
                Console.Error.WriteLine(error.Result);
            }
        }
    }
}