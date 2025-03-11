namespace ShortBox.Google;

internal interface IDriveServiceFactory 
{
    DriveService GetDriveService();
}

internal class DriveServiceFactory(
    IOptions<GoogleOptions> options) : IDriveServiceFactory
{
    public DriveService GetDriveService()
    {
        var credentials = GoogleCredential.FromJson(_opt.CredentialsJson)
            .CreateScoped(DriveService.Scope.Drive)
            .CreateWithUser(_opt.CredentialsUser);

        return new DriveService(new()
        {
            HttpClientInitializer = credentials,
            ApplicationName = "ShortBox"
        });
    }

    private readonly GoogleOptions _opt = options.Value;
}
