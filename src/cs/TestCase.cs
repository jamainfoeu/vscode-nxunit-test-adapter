using System;

namespace TestRunner
{
    class TestCase : IComparable<TestCase>
    {
        public string Test { get; }
        public string Name { get; }
        public string Time { get; set; }
        public string Result { get; set; }
        public string Message { get; set; }
        public string Stack { get; set; }
        public string Source { get; set; }
        public string LineNumber { get; set; }

        public static implicit operator string(TestCase rhs)
        {
            return rhs.Name;
        }

        public static implicit operator TestCase(string rhs)
        {
            return new TestCase(rhs);
        }

        public TestCase(string name = null, string test = null, string time = null, string result = null,
            string message = null, string stackTrace = null)
        {
            Name = name;
            Test = test;
            Time = time;
            Result = result;
            Message = message;
            Stack = stackTrace;
        }

        public int CompareTo(TestCase testCase)
        {
            // A null value means that this object is greater.
            if (testCase == null)
                return 1;
            else
                return String.Compare(Name, testCase.Name, StringComparison.Ordinal);
        }

        public void Print(RunMode runMode)
        {
            var name = Test ?? Name;

            if (runMode != RunMode.Discover)
            {
                Console.WriteLine(
                    "{0};{1};{2};{3};{4};{5}",
                    name, Result, Time, LineNumber, Source,
                    Message?.Replace("\r", "").Replace("\n", ", ")
                    );
            }
            else
                Console.WriteLine("{0};{1};{2}", name, Source, LineNumber);
        }
    }
}