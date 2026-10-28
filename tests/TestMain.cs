using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TVCleaner.Tests
{
    /// <summary>Runs every public static parameterless method named Test* in this namespace.</summary>
    public static class TestMain
    {
        public static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            int passed = 0, failed = 0;
            var methods = typeof(TestMain).Assembly.GetTypes()
                .Where(t => t.Namespace == "TVCleaner.Tests")
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.Name.StartsWith("Test") && m.GetParameters().Length == 0)
                .OrderBy(m => m.DeclaringType.Name + "." + m.Name);
            foreach (MethodInfo m in methods)
            {
                string name = m.DeclaringType.Name + "." + m.Name;
                try
                {
                    m.Invoke(null, null);
                    passed++;
                    Console.WriteLine("PASS " + name);
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine("FAIL " + name + ": " + e.InnerException.Message);
                }
            }
            Console.WriteLine(string.Format("Итого: {0} прошло, {1} упало", passed, failed));
            return failed;
        }
    }
}
