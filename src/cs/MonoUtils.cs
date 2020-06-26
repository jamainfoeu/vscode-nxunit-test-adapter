using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace TestRunner
{
    public class MonoUtils
    {

        internal static List<TestCase> Monoc(string filename, List<TestCase> lis)
        {
            List<TestCase> methods = new List<TestCase>();

            string exactPath = Path.GetFullPath(filename);
            Assembly testdll = Assembly.LoadFile(exactPath);
            Mono.Cecil.ReaderParameters readerParameters =
                new Mono.Cecil.ReaderParameters {ReadSymbols = true};
            Mono.Cecil.AssemblyDefinition assemblyDefinition;
            try
            {
                assemblyDefinition =
                    Mono.Cecil.AssemblyDefinition.ReadAssembly(filename, readerParameters);
            }
            catch (Exception)
            {
                readerParameters = new Mono.Cecil.ReaderParameters {ReadSymbols = false};
                assemblyDefinition =
                    Mono.Cecil.AssemblyDefinition.ReadAssembly(filename, readerParameters);
            }

            Mono.Cecil.ModuleDefinition module = assemblyDefinition.MainModule;
            methods = ProcessTypes(module.Types, methods, lis);
            methods.Sort();

            return methods;
        }

        private static List<TestCase> ProcessTypes(Collection<TypeDefinition> Types,
            List<TestCase> methods, List<TestCase> lis)
        {
            foreach (Mono.Cecil.TypeDefinition type in Types)
            {
                if (type.NestedTypes != null)
                    methods = ProcessTypes(type.NestedTypes, methods, lis);

                if (!type.IsPublic && !type.IsNested)
                    continue;

                foreach (Mono.Cecil.MethodDefinition method in type.Methods)
                {
                    var str = (type.FullName + '.' + method.Name).Replace('/', '+');
                    var f = lis.Find(x => x.Name == str);

                    if (f != null)
                        methods = AddTestCase(methods, method, type.FullName);
                }
            }

            return methods;
        }

        static List<TestCase> AddTestCase(List<TestCase> tests,
            Mono.Cecil.MethodDefinition method, string c)
        {
            if (method != null)
            {
                var d = CommonUtils.GetDebugInfo(method.Body);

                var s = c.Replace('/', '+');
                TestCase t = new TestCase(s + '.' + method.Name);
                if (d != null && d.Item1 != string.Empty && d.Item2 != -1)
                {
                    t.Source = d.Item1;
                    t.LineNumber = d.Item2.ToString();
                }

                tests.Add(t);
            }

            return tests;
        }
    }
}