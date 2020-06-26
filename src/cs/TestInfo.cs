using System;
using System.Collections.Generic;

namespace TestRunner
{
    class TestInfo
    {
        public string Type { get; set; }
        public string ModuleName { get; set; }
        public List<TestCase> TestCases { get; set; }

        public void Print(RunMode runMode)
        {
            if (runMode == RunMode.Discover)
                Console.WriteLine("{0};{1}", Type, ModuleName);

            foreach(var testCase in TestCases) {
                testCase.Print(runMode);
            }
            Console.WriteLine();
        }
    }
}