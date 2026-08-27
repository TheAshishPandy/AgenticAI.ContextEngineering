// AgenticAI.ContextEngineering.Demo/Services/DemoRunner.cs
using AgenticAI.ContextEngineering.Demo.Demos;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Services
{
    public class DemoRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly List<IDemo> _demos;

        public DemoRunner(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _demos = new List<IDemo>();
            RegisterDemos();
        }

        private void RegisterDemos()
        {
            // Register all demos in order
            AddDemo<KVCacheDemo>();
            AddDemo<TokenCacheDemo>();
            AddDemo<AIResponseDemo>();
            AddDemo<SearchDemo>();
            AddDemo<FAQDemo>();
            AddDemo<TokenOptimizedDemo>();
            AddDemo<StatisticsDemo>();
            AddDemo<ClearCacheDemo>();
        }

        private void AddDemo<T>() where T : IDemo
        {
            try
            {
                var demo = _serviceProvider.GetService<T>();
                if (demo != null)
                {
                    _demos.Add(demo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ Failed to register {typeof(T).Name}: {ex.Message}");
            }
        }

        public async Task RunAllDemosAsync()
        {
            Console.WriteLine("\n" + new string('═', 70));
            Console.WriteLine("  🚀 AGENTICAI CONTEXT ENGINEERING - COMPLETE DEMO");
            Console.WriteLine(new string('═', 70));

            var totalDemos = _demos.Count;
            var passedDemos = 0;
            var failedDemos = 0;

            foreach (var demo in _demos)
            {
                Console.WriteLine($"\n📌 {demo.Name}");
                Console.WriteLine($"   {demo.Description}");
                Console.WriteLine($"   Status: {demo.ConfigurationStatus}");

                if (!demo.IsConfigured)
                {
                    Console.WriteLine($"   ⚠️ Skipping - Not configured");
                    continue;
                }

                try
                {
                    await demo.RunAsync();
                    passedDemos++;
                    Console.WriteLine($"   ✅ {demo.Name} completed successfully");
                }
                catch (Exception ex)
                {
                    failedDemos++;
                    Console.WriteLine($"   ❌ {demo.Name} failed: {ex.Message}");
                }
            }

            Console.WriteLine("\n" + new string('═', 70));
            Console.WriteLine($"  📊 Summary: {passedDemos} passed, {failedDemos} failed, {totalDemos - passedDemos - failedDemos} skipped");
            //Console.WriteLine($"  {+ (failedDemos == 0 ? " SUCCESSFULLY!" : " WITH ERRORS")});
            Console.WriteLine(new string('═', 70));
        }

        public async Task RunDemoAsync<T>() where T : IDemo
        {
            var demo = _demos.FirstOrDefault(d => d is T);
            if (demo == null)
            {
                Console.WriteLine($"❌ Demo {typeof(T).Name} not found");
                return;
            }

            Console.WriteLine($"\n📌 {demo.Name}");
            Console.WriteLine($"   {demo.Description}");
            Console.WriteLine($"   Status: {demo.ConfigurationStatus}");

            if (!demo.IsConfigured)
            {
                Console.WriteLine($"   ⚠️ Skipping - Not configured");
                return;
            }

            await demo.RunAsync();
        }

        public void ListDemos()
        {
            Console.WriteLine("\n📋 Available Demos:");
            Console.WriteLine(new string('─', 40));

            foreach (var demo in _demos)
            {
                var status = demo.IsConfigured ? "✅" : "❌";
                Console.WriteLine($"  {status} {demo.Name}");
                Console.WriteLine($"     {demo.Description}");
            }
        }
    }
}