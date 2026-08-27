// AgenticAI.ContextEngineering.Demo/Demos/IDemo.cs
using System.Threading.Tasks;

namespace AgenticAI.ContextEngineering.Demo.Demos
{
    public interface IDemo
    {
        string Name { get; }
        string Description { get; }
        Task RunAsync();
        bool IsConfigured { get; }
        string ConfigurationStatus { get; }
    }
}