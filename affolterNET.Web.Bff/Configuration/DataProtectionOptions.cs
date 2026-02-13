using affolterNET.Web.Core.Configuration;
using affolterNET.Web.Core.Models;
using affolterNET.Web.Core.Options;

namespace affolterNET.Web.Bff.Configuration;

/// <summary>
/// Data Protection key persistence configuration options.
/// When Enabled, keys are persisted to Azure Blob Storage so they survive container restarts.
/// </summary>
public class DataProtectionOptions : IConfigurableOptions<DataProtectionOptions>
{
    public static string SectionName => "affolterNET:Web:DataProtection";

    public static DataProtectionOptions CreateDefaults(AppSettings settings)
    {
        return new DataProtectionOptions(settings);
    }

    public DataProtectionOptions() : this(new AppSettings())
    {
    }

    private DataProtectionOptions(AppSettings settings)
    {
        Enabled = false;
        ApplicationName = string.Empty;
        StorageAccountName = string.Empty;
        ManagedIdentityClientId = string.Empty;
        ConnectionString = string.Empty;
        ContainerName = "dataprotection";
        BlobName = "keys.xml";
    }

    public void CopyTo(DataProtectionOptions target)
    {
        target.Enabled = Enabled;
        target.ApplicationName = ApplicationName;
        target.StorageAccountName = StorageAccountName;
        target.ManagedIdentityClientId = ManagedIdentityClientId;
        target.ConnectionString = ConnectionString;
        target.ContainerName = ContainerName;
        target.BlobName = BlobName;
    }

    /// <summary>
    /// Enable persistent Data Protection keys in Azure Blob Storage.
    /// When false (default), ASP.NET Core uses ephemeral in-memory keys.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Application name for Data Protection key isolation.
    /// All instances sharing keys must use the same name.
    /// Required when Enabled = true.
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// Azure Storage account name (used with Managed Identity).
    /// Either StorageAccountName or ConnectionString must be set when Enabled.
    /// </summary>
    public string StorageAccountName { get; set; }

    /// <summary>
    /// Managed Identity client ID for Azure Storage access.
    /// Required when using StorageAccountName (not ConnectionString).
    /// </summary>
    [Sensible]
    public string ManagedIdentityClientId { get; set; }

    /// <summary>
    /// Azure Storage connection string (alternative to Managed Identity).
    /// Used when StorageAccountName is empty.
    /// </summary>
    [Sensible]
    public string ConnectionString { get; set; }

    /// <summary>
    /// Blob container name. Default: "dataprotection"
    /// </summary>
    public string ContainerName { get; set; }

    /// <summary>
    /// Blob name within the container. Default: "keys.xml"
    /// </summary>
    public string BlobName { get; set; }
}
