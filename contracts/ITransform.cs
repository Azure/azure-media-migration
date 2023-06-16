
namespace AMSMigrate.Contracts
{
    class MigrationResult
    {
        public MigrationResult(MigrationStatus status)
        {
            Status = status;
        }

        public MigrationStatus Status { get; private set; }

        public static implicit operator MigrationResult(MigrationStatus status) 
            => new MigrationResult(status);
    }

    interface ITransform<in T> 
    {
        /// <summary>
        /// Run the transform.
        /// </summary>
        /// <param name="resource">The resource to be transformed</param>
        Task<MigrationResult> RunAsync(T resource, CancellationToken cancellationToken);
    }
}
