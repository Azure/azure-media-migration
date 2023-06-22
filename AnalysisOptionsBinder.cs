using AMSMigrate.Contracts;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class AnalysisOptionsBinder : BinderBase<AnalysisOptions>
    {
        private readonly Option<AnalysisType> _analysisType = new(
            aliases: new[] { "-t", "--analysis-type" },
            () => AnalysisType.Summary,
            description: @"The kind of analysis to do.
Summary - Summary of migration
Detailed - A detailed classification of assets,
Report - A migration report")
            {
                IsRequired = false
            };

        const int DefaultBatchSize = 5;
        private readonly Option<int> _batchSize = new(
            aliases: new[] { "--batch-size", "-b" },
            () => DefaultBatchSize,
            description: @"Batch size for parallel processing.");

        public AnalysisOptions GetValue(BindingContext context) => GetBoundValue(context);

        public Command GetCommand(string name, string description)
        {
            var command = new Command(name, description);
            command.AddOption(_analysisType);
            command.AddOption(_batchSize);
            return command;
        }

        protected override AnalysisOptions GetBoundValue(BindingContext bindingContext)
        {
            return new AnalysisOptions(
                bindingContext.ParseResult.GetValueForOption(_analysisType),
                bindingContext.ParseResult.GetValueForOption(_batchSize)
            );
        }
    }
}
