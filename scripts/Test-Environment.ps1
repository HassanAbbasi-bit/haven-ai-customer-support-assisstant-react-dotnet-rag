$ErrorActionPreference = 'Stop'
foreach ($name in @('dotnet', 'node', 'npm.cmd', 'git', 'docker', 'ollama')) {
    $tool = Get-Command $name -ErrorAction SilentlyContinue
    if ($tool) { Write-Output "$name : $($tool.Source)" }
    else { Write-Output "$name : NOT FOUND on PATH (does not prove absent everywhere)" }
}
dotnet --list-sdks
node --version
npm.cmd --version
git --version
if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker compose version
    docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) { Write-Output 'Docker daemon inspection failed; Linux container mode is unverified.' }
}
if (Get-Command ollama -ErrorAction SilentlyContinue) {
    ollama --version
    ollama list
}
try {
    Get-CimInstance Win32_ComputerSystem | Select-Object TotalPhysicalMemory, SystemType
    Get-CimInstance Win32_Processor | Select-Object Name, NumberOfLogicalProcessors
    Get-CimInstance Win32_VideoController | Select-Object Name, AdapterRAM
}
catch { Write-Output "Hardware inspection unavailable: $($_.Exception.GetType().Name)" }
Get-PSDrive -PSProvider FileSystem | Select-Object Name, Free, Used
