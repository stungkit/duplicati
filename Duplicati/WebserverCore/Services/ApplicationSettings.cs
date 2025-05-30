
using Duplicati.Library.AutoUpdater;
using Duplicati.Library.Interface;
using Duplicati.WebserverCore.Abstractions;
namespace Duplicati.WebserverCore.Services;

/// <summary>
/// Application settings for the Duplicati server
/// </summary>
public class ApplicationSettings : IApplicationSettings
{
    /// <summary>
    /// The application exit event
    /// </summary>
    private readonly ManualResetEvent _applicationExitEvent = new ManualResetEvent(false);
    /// <summary>
    /// The folder where Duplicati data is stored
    /// </summary>
    private readonly string _dataFolder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationSettings"/> class.
    /// </summary>
    public ApplicationSettings()
    {
        _dataFolder = DataFolderManager.GetDataFolder(DataFolderManager.AccessMode.ReadWritePermissionSet);
    }

    /// <inheritdoc />
    public bool SettingsEncryptionKeyProvidedExternally { get; set; }

    /// <inheritdoc />
    public Action? StartOrStopUsageReporter { get; set; }

    /// <inheritdoc />
    public string DataFolder => _dataFolder;

    /// <inheritdoc />
    public string Origin { get; set; } = "Server";

    /// <inheritdoc />
    public ManualResetEvent ApplicationExitEvent => _applicationExitEvent;

    /// <inheritdoc />
    public ISecretProvider? SecretProvider { get; set; }
}