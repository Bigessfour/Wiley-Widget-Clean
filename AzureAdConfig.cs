using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WileyWidget;

/// <summary>
/// Configuration class for Azure AD settings
/// </summary>
public class AzureAdConfig
{
    /// <summary>
    /// The Azure AD authority URL
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The Azure AD client (application) ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The Azure AD tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// List of known authorities for custom domains
    /// </summary>
    public List<string> KnownAuthorities { get; set; } = new List<string>();
}