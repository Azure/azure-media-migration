using AMSMigrate.Contracts;
using Azure;
using Azure.Core;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager.Media;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Threading;
using System.Threading.Channels;

namespace AMSMigrate.Ams
{
    abstract class BaseMigrator
    {
        public const int PAGE_SIZE = 1024;

        protected readonly AzureResourceProvider _resourceProvider;
        protected readonly GlobalOptions _globalOptions;
        protected readonly MetricsQueryClient _metricsQueryClient;
        protected readonly IAnsiConsole _console;
        protected readonly ILogger _logger;

        public BaseMigrator(
            GlobalOptions options,
            IAnsiConsole console,
            TokenCredential credential,
            ILogger logger)
        {
            _globalOptions = options;
            _console = console;
            _logger = logger;
            _resourceProvider = new AzureResourceProvider(credential, options);
            _metricsQueryClient = new MetricsQueryClient(credential);
        }

        public abstract Task MigrateAsync(CancellationToken cancellationToken);

       

        protected async Task MigrateInParallel<T>(
            IAsyncEnumerable<T> values,
            IEnumerable<T>? filteredList,
            Func<T, CancellationToken, ValueTask> processItem,
            int batchSize = 5,
            CancellationToken cancellationToken = default) where T : notnull
        {
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = batchSize
            };
            if (filteredList != null)
            {
                // When the filtered List is already generated,
                // Take it as higher priority, no need to do extra enumeration on the pageable assets.
                await Parallel.ForEachAsync(filteredList, options, processItem);
            }
            else
            {
                await Parallel.ForEachAsync(values, options, processItem);
            }
        }
         protected async Task<(bool, MediaServicesAccountResource?)> IsAMSAccountAsync(string accountName, CancellationToken cancellationToken)
    {  
        MediaServicesAccountResource? amsAccount = null;

        try
        {
            amsAccount = await _resourceProvider.GetMediaAccountAsync(accountName, cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is OutOfMemoryException) throw;  // It is a fatal error.

            // For any other exception, swallow the exception, treat it as not-AMS account, 
            // The caller then has a chance to treat it as storage account and try it again,
            // if it is still failed, the caller will throw exception appropriately.
        }

        return (amsAccount != null, amsAccount);
    }

        protected async Task<double> GetStorageBlobMetricAsync(ResourceIdentifier accountId, CancellationToken cancellationToken)
        {
            return await QueryMetricAsync(
                $"{accountId}/blobServices/default",
                "ContainerCount",
                cancellationToken);
        }

        protected async Task<double> QueryMetricAsync(
            string resourceId,
            string metricName,
            CancellationToken cancellationToken)
        {
            var options = new MetricsQueryOptions
            {
                Granularity = TimeSpan.FromHours(1),
                TimeRange = new QueryTimeRange(TimeSpan.FromHours(6))
            };
            MetricsQueryResult queryResult = await _metricsQueryClient.QueryResourceAsync(
                resourceId,
                new[] { metricName },
                options,
                cancellationToken: cancellationToken);
            var metric = queryResult.Metrics[0];
            var series = metric.TimeSeries[metric.TimeSeries.Count - 1];
            var totalAssets = series.Values.LastOrDefault(v => v.Average != null)?.Average ?? 0.0;
            return totalAssets;
        }

        protected async Task ShowProgressAsync(
            string description,
            string unit,
            double totalValue,
            ChannelReader<double> reader,
            CancellationToken cancellationToken)
        {
            await _console
            .Progress()
            .AutoRefresh(true)
            .AutoClear(true)
            .HideCompleted(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new StatusColumn(unit),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async context =>
            {
                var task = context.AddTask(description, maxValue: totalValue);
                context.Refresh();
                await foreach (var value in reader.ReadAllAsync(cancellationToken))
                {
                    if (value > task.MaxValue)
                    {
                        task.MaxValue = value;
                    }
                    task.Value = value;
                    _logger.LogDebug("{description}: {current}/{total} {unit}", description, value, totalValue, unit);
                    context.Refresh();
                }
            });
        }

        protected string? GetAssetResourceFilter(string? filterOption, DateTimeOffset? creationTimeStart, DateTimeOffset? creationTimeEnd)
        {
            var resourceFilter = filterOption;

            if (creationTimeStart != null || creationTimeEnd != null)
            {
                // When --creation-time-start or --create-time-end option is set, 
                // Convert it to the filter format and combine with the current filter option's settings.

                var startTime = creationTimeStart?.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var endTime = creationTimeEnd?.ToString("yyyy-MM-ddTHH:mm:ssZ");

                if (startTime != null)
                {
                    if (resourceFilter != null)
                    {
                        resourceFilter += " and ";
                    }

                    resourceFilter += $"properties/created ge {startTime}";
                }

                if (endTime != null)
                {
                    if (resourceFilter != null)
                    {
                        resourceFilter += " and ";
                    }

                    resourceFilter += $"properties/created le {endTime}";
                }
            }

            return resourceFilter;
        }

    }
}
