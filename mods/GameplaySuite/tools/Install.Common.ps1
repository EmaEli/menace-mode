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