namespace AMSMigrate.Contracts
{
    public class MigrationResult
    {
        public MigrationResult(MigrationStatus status)
        {
            Status = status;
        }

        public MigrationStatus Status { get; protected set; }

        public static implicit operator MigrationResult(MigrationStatus status)
            => new MigrationResult(status);
    }

    interface ITransform<in T, TResult> where TResult : MigrationResult
    {
        /// <summary>
        /// Run the transform.
        /// </summary>
        /// <param name="resource">The resource to be transformed</param>
        Task<TResult> RunAsync(T resource, CancellationToken cancellationToken);
    }

    interface ITransform<in T> : ITransform<T, MigrationResult>
    {
    }
}
