<#
.SYNOPSIS
    Bereitstellungs- und Prüfskript für die 64-Bit-Logitech-SDK-Wrapper-DLL
    (Native\x64\LogitechSteeringWheelEnginesWrapper.dll).

.DESCRIPTION
    Prüft, ob eine gültige 64-Bit-DLL (AMD64 / PE 0x8664) im Projektordner liegt,
    sucht optional in lokalen Ordnern oder lädt die DLL auf ausdrücklichen Wunsch
    (-DownloadFromMirror) aus einem öffentlichen GitHub-SDK-Mirror herunter.

.PARAMETER LocalPath
    Pfad zu einer lokalen LogitechSteeringWheelEnginesWrapper.dll oder zu einem
    entpackten Logitech-SDK-/Projektordner.

.PARAMETER DownloadFromMirror
    Lädt die 64-Bit-DLL aus einem öffentlichen GitHub-Mirror herunter und prüft
    anschließend Dateigröße und PE-Architektur (AMD64).

.PARAMETER Force
    Überschreibt eine bereits vorhandene DLL im Zielordner.
#>
[CmdletBinding()]
param(
    [string]$LocalPath = '',
    [switch]$DownloadFromMirror,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$targetDir = Join-Path $repoRoot 'Native\x64'
$targetDll = Join-Path $targetDir 'LogitechSteeringWheelEnginesWrapper.dll'

function Test-IsPeX64Dll {
    param([Parameter(Mandatory = $true)][string]$FilePath)

    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
        return $false
    }

    $fileInfo = Get-Item -LiteralPath $FilePath
    if ($fileInfo.Length -lt 10240) {
        return $false
    }

    $stream = [System.IO.File]::OpenRead($FilePath)
    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        $dosMagic = $reader.ReadUInt16()
        if ($dosMagic -ne 0x5A4D) {
            return $false
        }

        $stream.Seek(0x3C, [System.IO.SeekOrigin]::Begin) | Out-Null
        $peOffset = $reader.ReadInt32()
        if ($peOffset -le 0 -or $peOffset -gt ($fileInfo.Length - 24)) {
            return $false
        }

        $stream.Seek($peOffset, [System.IO.SeekOrigin]::Begin) | Out-Null
        $peSignature = $reader.ReadUInt32()
        if ($peSignature -ne 0x00004550) {
            return $false
        }

        $machine = $reader.ReadUInt16()
        # 0x8664 = IMAGE_FILE_MACHINE_AMD64
        return ($machine -eq 0x8664)
    }
    finally {
        $stream.Dispose()
    }
}

function Copy-ValidatedDll {
    param([Parameter(Mandatory = $true)][string]$SourcePath)

    if (-not (Test-IsPeX64Dll -FilePath $SourcePath)) {
        throw "Die Datei '$SourcePath' ist keine gültige 64-Bit-PE-DLL (AMD64)."
    }

    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $targetDll -Force
    Write-Host "[OK] 64-Bit-DLL erfolgreich installiert: $targetDll" -ForegroundColor Green
}

if ((Test-Path -LiteralPath $targetDll) -and (-not $Force)) {
    if (Test-IsPeX64Dll -FilePath $targetDll) {
        Write-Host "[OK] Gültige 64-Bit-DLL bereits vorhanden: $targetDll" -ForegroundColor Green
        exit 0
    }
    else {
        Write-Warning "Vorhandene Datei unter '$targetDll' ist keine gültige 64-Bit-DLL und wird ersetzt."
    }
}

# 1. Explizit angegebener Pfad
if (-not [string]::IsNullOrWhiteSpace($LocalPath)) {
    if (Test-Path -LiteralPath $LocalPath -PathType Leaf) {
        Copy-ValidatedDll -SourcePath $LocalPath
        exit 0
    }
    elseif (Test-Path -LiteralPath $LocalPath -PathType Container) {
        $candidates = Get-ChildItem -LiteralPath $LocalPath -Filter 'LogitechSteeringWheelEnginesWrapper.dll' -Recurse -ErrorAction SilentlyContinue
        foreach ($candidate in $candidates) {
            if ($candidate.FullName -match 'x64|64') {
                Copy-ValidatedDll -SourcePath $candidate.FullName
                exit 0
            }
        }
        foreach ($candidate in $candidates) {
            if (Test-IsPeX64Dll -FilePath $candidate.FullName) {
                Copy-ValidatedDll -SourcePath $candidate.FullName
                exit 0
            }
        }
    }
    throw "Im angegebenen Pfad '$LocalPath' wurde keine gültige 64-Bit-DLL gefunden."
}

# 2. Lokale Standardpfade durchsuchen
$searchRoots = @(
    (Join-Path $env:USERPROFILE 'Downloads'),
    'C:\Program Files\LGHUB',
    'C:\Program Files\Logitech Gaming Software'
)

foreach ($root in $searchRoots) {
    $rootExists = $false
    try {
        $rootExists = Test-Path -LiteralPath $root -PathType Container -ErrorAction SilentlyContinue
    }
    catch {
        $rootExists = $false
    }

    if (-not $rootExists) {
        continue
    }

    $found = Get-ChildItem -LiteralPath $root -Filter 'LogitechSteeringWheelEnginesWrapper.dll' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { Test-IsPeX64Dll -FilePath $_.FullName } |
        Select-Object -First 1

    if ($null -ne $found) {
        Write-Host "[INFO] Lokale 64-Bit-DLL gefunden: $($found.FullName)" -ForegroundColor Cyan
        Copy-ValidatedDll -SourcePath $found.FullName
        exit 0
    }
}

# 3. Optionaler Download aus öffentlichem GitHub-SDK-Mirror
if ($DownloadFromMirror) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("LogitechWrapper_" + [Guid]::NewGuid().ToString('N') + ".dll")

    $mirrors = @(
        'https://raw.githubusercontent.com/tud-hri/logitech_steering_wheel/master/logitech_steering_wheel/dll/x64/LogitechSteeringWheelEnginesWrapper.dll',
        'https://raw.githubusercontent.com/cengizozel/LogiDrivePy/master/logidrivepy/dll/LogitechSteeringWheelEnginesWrapper.dll'
    )

    foreach ($url in $mirrors) {
        try {
            Write-Host "[INFO] Lade x64-Wrapper-DLL von Mirror: $url" -ForegroundColor Cyan
            Invoke-WebRequest -Uri $url -OutFile $tempFile -UseBasicParsing
            if (Test-IsPeX64Dll -FilePath $tempFile) {
                Copy-ValidatedDll -SourcePath $tempFile
                Remove-Item -LiteralPath $tempFile -ErrorAction SilentlyContinue
                exit 0
            }
            else {
                Write-Warning "Heruntergeladene Datei von '$url' ist keine gültige 64-Bit-PE-DLL."
            }
        }
        catch {
            Write-Warning "Mirror fehlgeschlagen ($url): $($_.Exception.Message)"
        }
        finally {
            if (Test-Path -LiteralPath $tempFile) {
                Remove-Item -LiteralPath $tempFile -ErrorAction SilentlyContinue
            }
        }
    }

    throw "Keiner der konfigurierten SDK-Mirrors lieferte eine gültige 64-Bit-DLL."
}

Write-Host ""
Write-Host "[HINWEIS] LogitechSteeringWheelEnginesWrapper.dll (x64) wurde noch nicht bereitgestellt." -ForegroundColor Yellow
Write-Host "So erhalten Sie die offizielle DLL:"
Write-Host "  1. Offizielles Logitech Steering Wheel SDK über das Logitech G Developer Lab / Unity Asset Store herunterladen."
Write-Host "  2. Aus dem SDK-Archiv die 64-Bit-Datei '\Lib\GameEnginesWrapper\x64\LogitechSteeringWheelEnginesWrapper.dll'"
Write-Host "     nach '$targetDll' kopieren — ODER ausführen:"
Write-Host "     .\setup_sdk.ps1 -LocalPath 'C:\Pfad\zum\SDK'"
Write-Host "  3. Alternativ Download aus bekanntem Open-Source-SDK-Mirror:"
Write-Host "     .\setup_sdk.ps1 -DownloadFromMirror"
Write-Host ""
Write-Host "Hinweis: Die Anwendung startet auch ohne DLL absturzfrei im Simulations-Modus." -ForegroundColor Cyan
exit 0
