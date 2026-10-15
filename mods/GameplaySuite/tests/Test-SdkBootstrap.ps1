# Runs production helpers, not an alternative installer. No game directory is used.
[CmdletBinding()]
param([string]$ToolsRoot, [string]$KitRoot, [switch]$InstallSdk)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if (-not $ToolsRoot) { $ToolsRoot = Join-Path $PSScriptRoot '..\tools' }
if (-not $KitRoot) { $KitRoot = Join-Path ([IO.Path]::GetTempPath()) ('MENACE-SdkTests-' + [guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $KitRoot -Force | Out-Null
$checks = 0
function Check([bool]$Passed, [string]$Name) {
    if (-not $Passed) { throw ('SDK bootstrap regression: ' + $Name) }
    $script:checks++
    Write-Host ('PASS: ' + $Name)
}
foreach ($name in @('Install.Common.ps1', 'Install-IntegratedSuite.ps1', 'Install.Deployment.ps1')) {
    $tokens = $null; $errors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $ToolsRoot $name), [ref]$tokens, [ref]$errors)
    Check ($errors.Count -eq 0) ('PowerShell parse: ' + $name)
}
. (Join-Path $ToolsRoot 'Install.Common.ps1')
$pin = (Get-Content -LiteralPath (Join-Path $ToolsRoot 'distribution.json') -Raw | ConvertFrom-Json).dotnetSdk
$saved = Save-BuildEnvironment
$persistent = @{}
foreach ($target in @('User','Machine')) {
    foreach ($key in @('PATH','DOTNET_ROOT')) { $persistent[$target + '/' + $key] = [Environment]::GetEnvironmentVariable($key, $target) }
}
$legacy = 'C:\Program Files\dotnet\dotnet.exe'
$oldSdks = ''
if (Test-Path -LiteralPath $legacy -PathType Leaf) { $oldSdks = (& $legacy --list-sdks | Out-String) }
try {
    $fixture = Join-Path $KitRoot 'hash-fixture.txt'
    [IO.File]::WriteAllText($fixture, 'non-executable download fixture')
    $hash = (Get-FileHash -LiteralPath $fixture -Algorithm SHA512).Hash
    Get-VerifiedDownload 'https://invalid.example/not-contacted' $fixture $hash 'SHA512'
    Check $true 'Verified cached download does not contact network'
    $rejected = $false
    try { Get-VerifiedDownload 'https://invalid.example/not-contacted' $fixture ('0' * 128) 'SHA512' }
    catch { $rejected = $_.Exception.Message -like 'Cached download integrity failed:*' }
    Check $rejected 'Corrupt archive rejected before extraction/network'
    function Check-DownloadedArchiveHash {
        function Invoke-WebRequest([switch]$UseBasicParsing, [string]$Uri, [string]$OutFile, [int]$TimeoutSec) {
            [IO.File]::WriteAllText($OutFile, 'non-executable download fixture')
        }
        $download = Join-Path $KitRoot 'download-fixture.txt'
        Get-VerifiedDownload 'https://invalid.example/not-contacted' $download $hash 'SHA512'
        Check (Test-Path -LiteralPath $download -PathType Leaf) 'Fresh verified download published after hash check'
        $bad = Join-Path $KitRoot 'bad-download-fixture.txt'
        $rejected = $false
        try { Get-VerifiedDownload 'https://invalid.example/not-contacted' $bad ('0' * 128) 'SHA512' }
        catch { $rejected = $_.Exception.Message -like 'Downloaded SHA512 mismatch*' }
        Check ($rejected -and -not (Test-Path -LiteralPath $bad)) 'Fresh corrupt download rejected before extraction'
    }
    Check-DownloadedArchiveHash
    $invalid = $pin | ConvertTo-Json | ConvertFrom-Json
    $invalid.installScriptUrl = 'https://invalid.example/script.ps1'
    $rejected = $false
    try { Initialize-PrivateDotnet (Join-Path $KitRoot 'invalid') $invalid }
    catch { $rejected = $_.Exception.Message -like '*official Microsoft*' }
    Check $rejected 'Unpinned script rejected before SDK setup'
    $diskPin = $pin | ConvertTo-Json | ConvertFrom-Json
    $diskPin.minimumFreeBytes = [long]::MaxValue
    $rejected = $false
    try { Initialize-PrivateDotnet (Join-Path $KitRoot 'low-disk') $diskPin }
    catch { $rejected = $_.Exception.Message -like 'Not enough free disk space*' }
    finally { Restore-BuildEnvironment $saved }
    Check $rejected 'Disk-space guard runs before downloads'
    $restored = Save-BuildEnvironment
    foreach ($key in $saved.Keys) { Check ($saved[$key] -ceq $restored[$key]) ('Process environment restored after failure: ' + $key) }
    if ($InstallSdk) {
        Initialize-PrivateDotnet $KitRoot $pin
        Check ($env:DOTNET_ROOT -eq (Join-Path $KitRoot 'tools\dotnet')) 'SDK root is private to kit'
        Check ($env:DOTNET_CLI_TELEMETRY_OPTOUT -eq '1') 'Telemetry disabled for process'
        Check ($env:DOTNET_NOLOGO -eq '1' -and $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE -eq '1') 'First-run output disabled for process'
        $archive = Join-Path $KitRoot ('tools\downloads\verified-feed\Sdk\' + $pin.version + '\dotnet-sdk-' + $pin.version + '-win-x64.zip')
        $stamp = (Get-Item -LiteralPath $archive).LastWriteTimeUtc
        function Check-CachedSdkWithoutNetwork {
            function Invoke-WebRequest { throw 'Cache regression: attempted network access.' }
            Initialize-PrivateDotnet $KitRoot $pin
        }
        Check-CachedSdkWithoutNetwork
        Check ((Get-Item -LiteralPath $archive).LastWriteTimeUtc -eq $stamp) 'Second bootstrap is idempotent; no network/extraction'
    }
}
finally { Restore-BuildEnvironment $saved }
$restored = Save-BuildEnvironment
foreach ($key in $saved.Keys) { Check ($saved[$key] -ceq $restored[$key]) ('Process environment restored after success: ' + $key) }
foreach ($target in @('User','Machine')) {
    foreach ($key in @('PATH','DOTNET_ROOT')) { Check ($persistent[$target + '/' + $key] -ceq [Environment]::GetEnvironmentVariable($key, $target)) ($target + ' environment unchanged: ' + $key) }
}
if (Test-Path -LiteralPath $legacy -PathType Leaf) { Check ($oldSdks -ceq (& $legacy --list-sdks | Out-String)) 'Machine-wide SDK list unchanged' }
Write-Host "SDK bootstrap behavior checks passed: $checks. Test files retained in $KitRoot"