namespace AMSMigrate.Contracts
{
    /// <summary>
    /// The packager type to use.
    /// </summary>
    public enum Packager
    {
        Ffmpeg, // Use ffmpeg to package the media.
        Shaka, // Use Google shaka-packager to package the media.
        None // Do not repackage but copy content as is.
    }
}
