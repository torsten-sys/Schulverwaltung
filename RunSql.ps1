$conn = New-Object System.Data.SqlClient.SqlConnection('Server=SQL.;Database=Schulverwaltung;User Id=sa;Password=Master99!;TrustServerCertificate=true')
$conn.Open()
$sql = Get-Content 'C:\Schulverwaltung\MeisterkursEinladung.sql' -Raw
$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql
try {
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host 'SQL executed successfully'
} catch {
    Write-Host "ERROR: $_"
}
$conn.Close()
