using Spectre.Console;
using Spectre.Console.Rendering;

namespace AMSMigrate.Ams
{
    internal class StatusColumn : ProgressColumn
    {
        private readonly string _unit;

        public StatusColumn(string unit)
        {
            _unit = unit;
        }

        public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        {
            return new Markup($"{task.Value}[grey]/[/]{task.MaxValue} [grey]{_unit}[/]");
        }
    }
}
