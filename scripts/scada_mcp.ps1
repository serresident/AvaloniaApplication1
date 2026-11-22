# Stdio MCP Proxy для внутрипроцессного моста ScadaDebugBridge
param(
    [string]$EndpointUrl = "http://127.0.0.1:5110/mcp"
)

$ErrorActionPreference = "Stop"

# Чтение JSON-RPC сообщений из stdin и пересылка через POST на локальный /mcp эндпоинт
$stdin = [System.Console]::In
$stdout = [System.Console]::Out

while ($true) {
    $line = $stdin.ReadLine()
    if ($line -eq $null) { break }
    if ([string]::IsNullOrWhiteSpace($line)) { continue }

    try {
        $response = Invoke-RestMethod -Uri $EndpointUrl -Method Post -Body $line -ContentType "application/json" -TimeoutSec 3
        $jsonResp = $response | ConvertTo-Json -Compress -Depth 10
        $stdout.WriteLine($jsonResp)
        $stdout.Flush()
    }
    catch {
        $errResp = @{
            jsonrpc = "2.0"
            error = @{
                code = -32000
                message = "ScadaDebugBridge connection error: $($_.Exception.Message)"
            }
            id = $null
        } | ConvertTo-Json -Compress
        $stdout.WriteLine($errResp)
        $stdout.Flush()
    }
}
