using System;
using WootMouseRemap.Tests;

namespace NativeTestRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🚀 Native Mode Integration Test Runner");
            Console.WriteLine("======================================");

            try
            {
                NativeModeIntegrationTests.RunAllTests();
                Console.WriteLine("\n🎉 All integration tests passed!");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n💥 Test runner failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }
    }
}