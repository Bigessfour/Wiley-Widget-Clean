param(
    [Parameter(Mandatory = $false)]
    [string]$ConnectionString = "Server=.\SQLEXPRESS;Database=WileyWidgetDev;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;"
)

Add-Type -AssemblyName System.Data

$connection = $null
try {
    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    $connection.Open()
    Write-Output "✅ Connected to $( $connection.DataSource )/$( $connection.Database ) successfully"
} catch {
    Write-Error $_
    exit 1
} finally {
    if ($connection) {
        $connection.Dispose()
    }
}
