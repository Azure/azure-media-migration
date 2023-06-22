namespace AMSMigrate.Contracts
{
    interface IMigrationTracker<T, TResult> where TResult : MigrationResult
    {
        Task<TResult> GetMigrationStatusAsync(T resource, CancellationToken cancellationToken);

        Task UpdateMigrationStatus(T resource, TResult result, CancellationToken cancellationToken);
    }
}
