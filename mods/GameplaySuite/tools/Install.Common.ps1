# Shared MEN-006 pattern: strict validation, immutable source snapshot, metadata checks.
Set-StrictMode -Version Latest

function Assert-Leaf([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file missing: $Path" }
}
function Assert-SafeName([string]$Name) {
    if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9._ -]*$' -or $Name -in @('.', '..')) {
        throw "Unsafe direct-child target name: $Name"
    }
}
function Assert-NotLink([string]$Path) {
    if ((Get-Item -LiteralPath $Path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Reparse point/symlink is not a safe install target: $Path"
    }
}
function Assert-GameClosed {
    if (Get-Process -Name Menace -ErrorAction SilentlyContinue) { throw 'Close MENACE before building/installing/rolling back.' }
}
function Assert-Hash([string]$Path, [string]$Expected) {
    Assert-Leaf $Path
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw "SHA256 mismatch: $Path"
    }
}
function Get-TreeInventory([string]$Path) {
    Assert-NotLink $Path
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        [pscustomobject]@{ Path = ''; Hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
        return
    }
    $base = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $queue = New-Object 'System.Collections.Generic.Queue[string]'
    $queue.Enqueue($Path)
    $entries = New-Object 'System.Collections.Generic.List[object]'
    while ($queue.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $queue.Dequeue() -Force) {
            Assert-NotLink $item.FullName
            if ($item.PSIsContainer) { $queue.Enqueue($item.FullName) }
            else {
                $entries.Add([pscustomobject]@{
                    Path = $item.FullName.Substring($base.Length).Replace('\', '/')
                    Hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
                })
            }
        }
    }
    $entries | Sort-Object Path
}
function Assert-Inventory([string]$Path, [object[]]$Expected) {
    $actual = @(Get-TreeInventory $Path)
    if ($actual.Count -ne $Expected.Count) { throw "File count changed: $Path" }
    $map = @{}
    foreach ($entry in $actual) { $map[$entry.Path] = $entry.Hash }
    foreach ($entry in $Expected) {
        if (-not $map.ContainsKey($entry.Path) -or $map[$entry.Path] -ne $entry.Hash) { throw "File integrity mismatch: $Path/$($entry.Path)" }
    }
}
function Assert-Bundle([string]$Root, $Kit) {
    if ($Kit.format -ne 1 -or @($Kit.files).Count -lt 4) { throw 'Missing/unsupported snapshot inventory.' }
    $sourceFiles = @{}
    foreach ($entry in $Kit.files) {
        $relative = [string]$entry.path
        if ($relative -match '(^|[/\\])\.\.([/\\]|$)|:' -or [IO.Path]::IsPathRooted($relative)) { throw 'Unsafe snapshot path.' }
        Assert-Hash (Join-Path $Root $relative) $entry.sha256
        if ($relative.StartsWith('source/')) {
            $key = $relative.Substring(7)
            if ($sourceFiles.ContainsKey($key)) { throw 'Duplicate source inventory entry.' }
            $sourceFiles[$key] = $entry.sha256
        }
    }
    $actual = @(Get-TreeInventory (Join-Path $Root 'source'))
    if ($actual.Count -ne $sourceFiles.Count) { throw 'Uninventoried source files; refusing build.' }
    foreach ($entry in $actual) {
        if (-not $sourceFiles.ContainsKey($entry.Path) -or $sourceFiles[$entry.Path] -ne $entry.Hash) { throw 'Source tree integrity mismatch.' }
    }
}
function Assert-Dll([string]$Path, [string]$Version, [string]$AssemblyVersion = '') {
    Assert-Leaf $Path
    $parts = @($Version.Split('.'))
    while ($parts.Count -lt 4) { $parts += '0' }
    $normal = ([version]($parts -join '.')).ToString(4)
    if (-not $AssemblyVersion) { $AssemblyVersion = $normal }
    $actual = [Reflection.AssemblyName]::GetAssemblyName($Path).Version.ToString()
    if ($actual -ne $AssemblyVersion) { throw "DLL AssemblyVersion $actual != $AssemblyVersion : $Path" }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($Path).FileVersion -ne $normal) { throw "DLL FileVersion mismatch: $Path" }
}
function Invoke-Dotnet([string[]]$BuildArguments) {
    & dotnet @BuildArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}

function Get-VerifiedDownload([string]$Uri, [string]$Path, [string]$Hash, [string]$Algorithm) {
    if (Test-Path -LiteralPath $Path) {
        Assert-NotLink $Path
        if ((Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash -ne $Hash) {
            throw "Cached download integrity failed: $Path. No archive was extracted."
        }
        Write-Host "Reusing verified download: $Path"
        return
    }
    $partial = $Path + '.partial-' + [guid]::NewGuid().ToString('N')
    Write-Host "Downloading pinned build input: $Uri"
    Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $partial -TimeoutSec 1200
    if ((Get-FileHash -LiteralPath $partial -Algorithm $Algorithm).Hash -ne $Hash) {
        throw "Downloaded $Algorithm mismatch; rejected file retained at $partial. No archive was extracted."
    }
    Move-Item -LiteralPath $partial -Destination $Path -ErrorAction Stop
    Write-Host "$Algorithm verified: $Path"
}

function Save-BuildEnvironment {
    $saved = @{}
    foreach ($name in @('PATH', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_NOLOGO', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_MULTILEVEL_LOOKUP', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES')) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    return $saved
}
function Restore-BuildEnvironment($Saved) {
    foreach ($name in $Saved.Keys) { [Environment]::SetEnvironmentVariable($name, $Saved[$name], 'Process') }
}

function Initialize-PrivateDotnet([string]$KitRoot, $Pin) {
    # The caller restores these process-local variables in finally. Never use User/Machine targets.
    if ($Pin.channel -ne '10.0' -or $Pin.version -notmatch '^10\.0\.1[0-9]{2}$' -or
        $Pin.archiveSha512 -notmatch '^[a-fA-F0-9]{128}$' -or $Pin.installScriptSha256 -notmatch '^[a-fA-F0-9]{64}$') {
        throw 'Missing/invalid pinned .NET 10 SDK metadata.'
    }
    $expectedArchive = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/' + $Pin.version + '/dotnet-sdk-' + $Pin.version + '-win-x64.zip'
    if ($Pin.archiveUrl -ne $expectedArchive -or
        $Pin.installScriptUrl -notmatch '^https://raw\.githubusercontent\.com/dotnet/install-scripts/[a-f0-9]{40}/src/dotnet-install\.ps1$') {
        throw 'SDK bootstrap requires pinned official Microsoft archive/script URLs.'
    }
    if (-not [Environment]::Is64BitProcess) { throw 'Use Windows x64 PowerShell for the private SDK.' }
    $tools = Join-Path $KitRoot 'tools'
    if (Test-Path -LiteralPath $tools) { Assert-NotLink $tools }
    $root = Join-Path $tools 'dotnet'
    if (Test-Path -LiteralPath $root) { Assert-NotLink $root }
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $env:DOTNET_ROOT = $root
    $env:DOTNET_ROOT_X64 = $root
    $env:PATH = $root + [IO.Path]::PathSeparator + $env:PATH
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:DOTNET_CLI_HOME = Join-Path $tools 'dotnet-home'
    $env:NUGET_PACKAGES = Join-Path $tools 'nuget-packages'
    $exe = Join-Path $root 'dotnet.exe'
    $versionFolder = Join-Path (Join-Path $root 'sdk') $Pin.version
    if (-not ((Test-Path -LiteralPath $exe -PathType Leaf) -and (Test-Path -LiteralPath $versionFolder -PathType Container))) {
        $drive = New-Object IO.DriveInfo ([IO.Path]::GetPathRoot([IO.Path]::GetFullPath($root)))
        if ($drive.AvailableFreeSpace -lt [long]$Pin.minimumFreeBytes) {
            throw 'Not enough free disk space for the private SDK archive, extraction copy and installed SDK (2 GiB required).'
        }
        Write-Host 'Private SDK disk-space check passed (2 GiB download/extraction allowance).'
        $downloads = Join-Path $tools 'downloads'
        if (Test-Path -LiteralPath $downloads) { Assert-NotLink $downloads }
        $feed = Join-Path $downloads 'verified-feed'
        $sdkFeed = Join-Path (Join-Path $feed 'Sdk') $Pin.version
        New-Item -ItemType Directory -Path $sdkFeed -Force | Out-Null
        $archive = Join-Path $sdkFeed ('dotnet-sdk-' + $Pin.version + '-win-x64.zip')
        $installer = Join-Path $downloads 'dotnet-install.ps1'
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Get-VerifiedDownload $Pin.installScriptUrl $installer $Pin.installScriptSha256 'SHA256'
        Get-VerifiedDownload $Pin.archiveUrl $archive $Pin.archiveSha512 'SHA512'
        # Microsoft's filesystem AzureFeed consumes ONLY our already hash-verified archive.
        Write-Host 'Installing with pinned Microsoft script from the SHA512-verified local feed.'
        & $installer -Channel $Pin.channel -Version $Pin.version -Architecture x64 -InstallDir $root -NoPath -AzureFeed $feed | Out-Host
    }
    else { Write-Host 'Pinned private SDK already present; no download/extraction required.' }
    Assert-Leaf $exe
    Assert-NotLink $exe
    Push-Location $root
    try {
        $actual = (& $exe --version | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $actual -ne $Pin.version) { throw "Private SDK version mismatch: expected $($Pin.version)." }
        Write-Host "Private dotnet --version: $actual"
    }
    finally { Pop-Location }
}

function Assert-NoUnknownDuplicates([string]$Mods, [string]$Stage, [string[]]$Names, [string[]]$Conflicts) {
    $knownDlls = @($Names | Where-Object { $_ -match '\.dll$' })
    foreach ($name in $Names) {
        $folder = Join-Path $Stage $name
        if (Test-Path -LiteralPath $folder -PathType Container) {
            $code = Join-Path $folder 'code'
            if (Test-Path -LiteralPath $code) { $knownDlls += @(Get-ChildItem -LiteralPath $code -Filter '*.dll' -File | ForEach-Object { $_.Name }) }
        }
        $knownDlls += "$name.dll"
    }
    foreach ($folder in Get-ChildItem -LiteralPath $Mods -Directory) {
        if ($Names -contains $folder.Name -or $Conflicts -contains $folder.Name) { continue }
        Assert-NotLink $folder.FullName
        $manifest = Join-Path $folder.FullName 'jiangyu.json'
        if (Test-Path -LiteralPath $manifest) {
            $other = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
            if ($Names -contains $other.name) { throw "Renamed duplicate package: $($folder.FullName). No mod was changed." }
        }
        foreach ($sub in @('', 'dlls', 'code')) {
            $path = $folder.FullName
            if ($sub) { $path = Join-Path $path $sub }
            if (-not (Test-Path -LiteralPath $path -PathType Container)) { continue }
            Assert-NotLink $path
            foreach ($dll in Get-ChildItem -LiteralPath $path -Filter '*.dll' -File) {
                if ($knownDlls -contains $dll.Name) { throw "Ambiguous duplicate DLL: $($dll.FullName). No mod was changed." }
            }
        }
    }
}