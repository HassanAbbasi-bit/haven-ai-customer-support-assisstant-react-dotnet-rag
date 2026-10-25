param([ValidateSet('Api','Web','All')][string]$Mode = 'All')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($port in $(if ($Mode -eq 'Api') { 5080 } elseif ($Mode -eq 'Web') { 5173 } else { 5080,5173 })) {
    $listeners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    foreach ($listener in $listeners) {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
        $expectedApi = Join-Path $root 'src\HavenSupport.Api\bin\'
        $expectedWeb = Join-Path $root 'src\haven-support-web\'
        $owned = ($port -eq 5080 -and $process.ExecutablePath -and $process.ExecutablePath.StartsWith($expectedApi, [StringComparison]::OrdinalIgnoreCase)) -or ($port -eq 5173 -and $process.CommandLine -and $process.CommandLine.IndexOf($expectedWeb, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        if (-not $owned) { Write-Warning "Port $port is owned by another application. It was left running."; continue }
        Stop-Process -Id $listener.OwningProcess
        Write-Output "Stopped this project's development service on port $port."
    }
}
Write-Output 'SQL/Qdrant containers and all persistent data were preserved.'
