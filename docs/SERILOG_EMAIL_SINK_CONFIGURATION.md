# Serilog Email Sink Configuration Guide

## ✅ Current Configuration Status

### Packages Installed
- ✅ `Serilog 4.2.0`
- ✅ `Serilog.Settings.Configuration 9.0.0` (**CRITICAL** - Required for JSON config)
- ✅ `Serilog.Sinks.Email 3.0.0`
- ✅ `Serilog.Sinks.Async 2.0.0`
- ✅ `Serilog.Sinks.File 7.0.0`
- ✅ `Serilog.Enrichers.Environment 3.0.1`
- ✅ `Serilog.Enrichers.Process 3.0.0`
- ✅ `Serilog.Enrichers.Thread 4.0.0`

## 📋 JSON Configuration (appsettings.json)

### Current Implementation
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Email",
        "Args": {
          "from": "${EMAIL_FROM_ADDRESS:errors@wileywidget.local}",
          "to": "${EMAIL_TO_ADDRESS:admin@wileywidget.local}",
          "host": "${EMAIL_SMTP_SERVER:localhost}",
          "port": "${EMAIL_SMTP_PORT:25}",
          "connectionSecurity": "None",
          "subject": "Wiley Widget Application Error - {MachineName}",
          "body": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {MachineName} {ProcessId}:{ThreadId}{NewLine}{Message:lj}{NewLine}{Exception}",
          "restrictedToMinimumLevel": "Error"
        }
      }
    ]
  }
}
```

### Valid Parameter Names (v3.0.0)
According to [Serilog.Sinks.Email source code](https://github.com/serilog/serilog-sinks-email/blob/dev/src/Serilog.Sinks.Email/LoggerConfigurationEmailExtensions.cs):

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `from` | string | (required) | Email address emails will be sent from |
| `to` | string | (required) | Email address(es) to send to (comma/semicolon separated) |
| `host` | string | (required) | SMTP server hostname |
| `port` | int | 25 | SMTP port |
| `connectionSecurity` | SecureSocketOptions enum | Auto | Security: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable |
| `credentials` | ICredentialsByHost | null | ⚠️ **Cannot be configured from JSON** |
| `subject` | string | "Log Messages" | Email subject template |
| `body` | string | "{Timestamp} [{Level}] {Message}{NewLine}{Exception}" | Email body template |
| `restrictedToMinimumLevel` | LogEventLevel | Minimum | Minimum log level |

## ⚠️ KNOWN LIMITATION: Credentials Configuration

### The Problem
`Serilog.Sinks.Email 3.0.0` changed the `credentials` parameter from simple username/password strings to an `ICredentialsByHost` interface. **This interface cannot be instantiated from JSON configuration.**

### GitHub Issues
- [Issue #136: Documentation on how to configure credentials from appsettings.json](https://github.com/serilog/serilog-sinks-email/issues/136)
- [Issue #76: Unable to use appsetting to configure credentials](https://github.com/serilog/serilog-sinks-email/issues/76)

## 🔧 Solutions & Workarounds

### Option 1: Use Code-Based Configuration (RECOMMENDED)

Create a custom configuration extension in `WpfHostingExtensions.cs`:

```csharp
using System.Net;
using MailKit.Security;
using Serilog.Sinks.Email;

// In ConfigureApplicationLogging method, AFTER ReadFrom.Configuration:
var emailConfig = builder.Configuration.GetSection("EmailSink");
if (emailConfig.Exists())
{
    var options = new EmailSinkOptions
    {
        From = emailConfig["From"] ?? "errors@wileywidget.local",
        To = emailConfig["To"] ?? "admin@wileywidget.local",
        Host = emailConfig["Host"] ?? "smtp.gmail.com",
        Port = int.Parse(emailConfig["Port"] ?? "587"),
        ConnectionSecurity = SecureSocketOptions.StartTlsWhenAvailable,
        Credentials = new NetworkCredential(
            emailConfig["Username"],
            emailConfig["Password"]
        ),
        Subject = new MessageTemplateTextFormatter(
            "Wiley Widget Error - {MachineName}",
            null
        ),
        Body = new MessageTemplateTextFormatter(
            "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}",
            null
        )
    };

    configuredLogger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.Email(
            options,
            batchingOptions: new() { BatchSizeLimit = 10, BufferingTimeLimit = TimeSpan.FromSeconds(30) },
            restrictedToMinimumLevel: LogEventLevel.Error
        )
        .CreateLogger();
}
```

**appsettings.json:**
```json
{
  "EmailSink": {
    "From": "${EMAIL_FROM_ADDRESS}",
    "To": "${EMAIL_TO_ADDRESS}",
    "Host": "${EMAIL_SMTP_SERVER}",
    "Port": "587",
    "Username": "${EMAIL_USERNAME}",
    "Password": "${EMAIL_PASSWORD}"
  }
}
```

### Option 2: Use smtp4dev for Testing (NO AUTH REQUIRED)

**Install smtp4dev:**
```powershell
dotnet tool install -g Rnwood.Smtp4dev
smtp4dev
```

**Configuration:**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Email",
        "Args": {
          "from": "test@wileywidget.local",
          "to": "admin@wileywidget.local",
          "host": "localhost",
          "port": 25,
          "connectionSecurity": "None",
          "subject": "Wiley Widget Error",
          "body": "{Timestamp} [{Level}] {Message}{NewLine}{Exception}",
          "restrictedToMinimumLevel": "Error"
        }
      }
    ]
  }
}
```

### Option 3: Use Environment Variables in Production

For production with authenticated SMTP, use **Option 1** with credentials from:
- **Azure Key Vault** (recommended)
- **User Secrets** (development)
- **Environment Variables** (container deployments)

## 🧪 Testing

### Test Email Sending
```csharp
Log.Error("Test error to trigger email notification");
```

### Check Self-Log
```powershell
Get-Content "logs\serilog-selflog.txt" -Tail 50
```

**Expected (Success):**
- No "Unable to find method" errors
- No "Cannot create instance of type 'System.Net.ICredentialsByHost'" errors

**Expected (No Credentials):**
- Email sink works for localhost/smtp4dev (no auth)
- For authenticated SMTP, you'll see connection errors but no configuration errors

## 📚 References

- [Serilog.Sinks.Email GitHub](https://github.com/serilog/serilog-sinks-email)
- [Serilog.Settings.Configuration GitHub](https://github.com/serilog/serilog-settings-configuration)
- [MailKit SecureSocketOptions](https://github.com/jstedfast/MailKit/blob/master/MailKit/Security/SecureSocketOptions.cs)

## 🔄 Migration Notes

### From Version 2.x to 3.0.0
**Breaking changes:**
- ❌ `fromEmail` → ✅ `from`
- ❌ `toEmail` → ✅ `to`
- ❌ `mailServer` → ✅ `host`
- ❌ `enableSsl` → ✅ `connectionSecurity` (enum)
- ❌ `networkCredential` (object) → ✅ `credentials` (ICredentialsByHost - **NOT JSON configurable**)
- ❌ `mailSubject` → ✅ `subject`
- ❌ `textBody` → ✅ `body`
- ❌ `batchPostingLimit`, `period` → ✅ Use `batchingOptions` parameter (code only)

---

**Last Updated:** October 13, 2025  
**Package Version:** Serilog.Sinks.Email 3.0.0  
**Status:** ✅ Configuration validated, credentials require code-based setup
