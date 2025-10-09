using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WileyWidget;

/// <summary>
/// Configuration class for Azure AD settings - follows Microsoft's official WPF desktop app pattern
/// Based on: https://learn.microsoft.com/en-us/entra/identity-platform/tutorial-desktop-wpf-dotnet-sign-in-build-app
/// </summary>
public class AzureAdConfig
{
    /// <summary>
    /// The Azure AD instance (e.g., https://login.microsoftonline.com/)
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// The Azure AD domain (optional, for single-tenant apps)
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// The Azure AD tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The Azure AD client (application) ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The Azure AD authority URL (computed from Instance and TenantId)
    /// </summary>
    public string Authority
    {
        get
        {
            if (!string.IsNullOrEmpty(Domain))
            {
                return $"{Instance.TrimEnd('/')}/{Domain}";
            }
            return $"{Instance.TrimEnd('/')}/{TenantId}";
        }
    }

    /// <summary>
    /// List of scopes to request (default: User.Read)
    /// </summary>
    public string[] Scopes { get; set; } = new[] { "User.Read" };

    /// <summary>
    /// Validates that the Azure AD configuration is properly set up
    /// </summary>
    /// <returns>A list of validation errors, empty if configuration is valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ClientId) || ClientId == "00000000-0000-0000-0000-000000000000")
        {
            errors.Add("AzureAd:ClientId must be configured with a valid application ID");
        }

        if (string.IsNullOrWhiteSpace(TenantId) || TenantId == "00000000-0000-0000-0000-000000000000")
        {
            errors.Add("AzureAd:TenantId must be configured with a valid tenant ID");
        }

        if (string.IsNullOrWhiteSpace(Instance))
        {
            errors.Add("AzureAd:Instance must be configured with a valid instance URL");
        }
        else if (!Uri.IsWellFormedUriString(Instance, UriKind.Absolute))
        {
            errors.Add("AzureAd:Instance must be a valid absolute URL");
        }

        if (Scopes == null || Scopes.Length == 0)
        {
            errors.Add("AzureAd:Scopes must contain at least one scope");
        }

        return errors;
    }

    /// <summary>
    /// Gets whether the configuration is valid
    /// </summary>
    public bool IsValid => Validate().Count == 0;
}