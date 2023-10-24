namespace AMSMigrate.Contracts
{
    internal interface IPackager
    {
        bool UsePipeForInput { get; }

        bool UsePipeForOutput { get; }

        bool UsePipeForManifests { get; }

        public IList<string> Inputs { get; }

        public IList<string> Outputs { get; }

        public IList<string> Manifests { get; }

        Task<bool> RunAsync(
            string workingDirectory,
            string[] inputs,
            string[] outputs,
            string[] manifests,
            CancellationToken cancellationToken);
    }

    interface IPackagerFactory
    {
        IPackager GetPackager(Manifest manifest);
    }
}
