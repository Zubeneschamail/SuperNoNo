param(
    [ValidateSet('idle', 'thinking', 'planning', 'working', 'coding', 'editing', 'building', 'testing', 'reviewing', 'success', 'warning', 'error', 'blocked')]
    [string]$State = 'working',

    [ValidateRange(0, 100)]
    [int]$Progress = 0,

    [Alias('Conclusion')]
    [string]$Message = ''
)

$statusFolder = Join-Path $env:LOCALAPPDATA 'DesktopPet'
$statusPath = Join-Path $statusFolder 'codex-progress.json'
New-Item -Path $statusFolder -ItemType Directory -Force | Out-Null

$status = [ordered]@{
    state = $State
    progress = $Progress
    updatedAt = (Get-Date).ToUniversalTime().ToString('o')
}

if ($Message) {
    $status.message = $Message
}

$json = $status | ConvertTo-Json -Depth 3
[System.IO.File]::WriteAllText($statusPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output $statusPath
