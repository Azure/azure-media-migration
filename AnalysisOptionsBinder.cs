using AMSMigrate.Contracts;
using System.CommandLine;
using System.CommandLine.Binding;

namespace AMSMigrate
{
    internal class AnalysisOptionsBinder : BinderBase<AnalysisOptions>
    {
        private readonly Option<string> _sourceAccount = new Option<string>(
             aliases: new[] { "--source-account-name", "-n" },
             description: "Azure Media Services Account or Storage account name.")
        {
            IsRequired = true,
            Arity = ArgumentArity.ExactlyOne
        };

        private readonly Option<string?> _filter = new Option<string?>(
            aliases: new[] { "--resource-filter", "-f" },
            description: @"An ODATA condition to filter the resources only when the source account is for media service.
e.g.: ""name eq 'asset1'"" to match an asset with name 'asset1'.
Visit https://learn.microsoft.com/en-us/azure/media-services/latest/filter-order-page-entities-how-to for more information.")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

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
            command.AddOption(_sourceAccount);
            command.AddOption(_filter);
            command.AddOption(_analysisType);
            command.AddOption(_batchSize);
            return command;
        }

        protected override AnalysisOptions GetBoundValue(BindingContext bindingContext)
        {
            return new AnalysisOptions(
                bindingContext.ParseResult.GetValueForOption(_sourceAccount)!,
                bindingContext.ParseResult.GetValueForOption(_filter),
                bindingContext.ParseResult.GetValueForOption(_analysisType),
                bindingContext.ParseResult.GetValueForOption(_batchSize)
            );
        }
    }
}
