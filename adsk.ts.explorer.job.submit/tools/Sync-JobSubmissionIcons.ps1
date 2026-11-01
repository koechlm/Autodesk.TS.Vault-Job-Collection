# Generates menu icons under Resources/Icons/{Light,Dark} for adsk.ts.explorer.job.submit.
# - Menu icons: one output-format glyph per command (16x16, aspect ratio preserved).
# - Resources/Icons/_formats: file-type icons (capture on a workstation with Shell file associations).
# - Resources/Icons/_apps: product icons (kept for reference; not used on menus).

param(
    [string]$VaultIconsDepot = "C:\Users\koechlm\OneDrive - Autodesk\Pictures\Libraries\Icons Vault\_2023_New-Icons\Vault Icons Depot"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not ([System.Management.Automation.PSTypeName]'ShellIconHelper').Type) {
    Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class ShellIconHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_SMALLICON = 0x000000001;
    public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
}
"@
}

$projectRoot = Split-Path $PSScriptRoot -Parent
$outRoot = Join-Path $projectRoot "Resources\Icons"
$sharedApps = Join-Path $outRoot "_apps"
$sharedFormats = Join-Path $outRoot "_formats"
New-Item -ItemType Directory -Force -Path $sharedApps | Out-Null
New-Item -ItemType Directory -Force -Path $sharedFormats | Out-Null

foreach ($theme in @("Light", "Dark")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $outRoot $theme) | Out-Null
}

function Copy-DepotIcon {
    param(
        [string]$RelativePath,
        [string]$Theme,
        [string]$TargetFileName
    )
    $source = Join-Path $VaultIconsDepot $RelativePath
    if (-not (Test-Path $source)) {
        return $null
    }
    return $source
}

function Save-LetterboxedIcon16 {
    param(
        [System.Drawing.Image]$Image,
        [string]$Path
    )
    $size = 16
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $scale = [Math]::Min($size / $Image.Width, $size / $Image.Height)
    $w = [int]($Image.Width * $scale)
    $h = [int]($Image.Height * $scale)
    $x = [int](($size - $w) / 2)
    $y = [int](($size - $h) / 2)
    $g.DrawImage($Image, $x, $y, $w, $h)
    $g.Dispose()

    $dir = Split-Path $Path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Write-MenuIconFromSource {
    param(
        [string]$SourcePath,
        [string]$Theme,
        [string]$MenuKey
    )
    if (-not (Test-Path $SourcePath)) {
        return $false
    }
    $dest = Join-Path $outRoot "$Theme\$MenuKey.png"
    $img = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        if ($img.Width -eq 16 -and $img.Height -eq 16) {
            Copy-Item -LiteralPath $SourcePath -Destination $dest -Force
        }
        else {
            Save-LetterboxedIcon16 -Image $img -Path $dest
        }
        return $true
    }
    finally {
        $img.Dispose()
    }
}

function Export-ExtensionIcon16 {
    param(
        [string]$Extension,
        [string]$TargetPath
    )
    $dummy = "sample$Extension"
    $shinfo = New-Object ShellIconHelper+SHFILEINFO
    $flags = [ShellIconHelper]::SHGFI_ICON -bor [ShellIconHelper]::SHGFI_SMALLICON -bor [ShellIconHelper]::SHGFI_USEFILEATTRIBUTES
    $result = [ShellIconHelper]::SHGetFileInfo(
        $dummy,
        [ShellIconHelper]::FILE_ATTRIBUTE_NORMAL,
        [ref]$shinfo,
        [uint32][System.Runtime.InteropServices.Marshal]::SizeOf($shinfo),
        $flags) | Out-Null

    if ($shinfo.hIcon -eq [IntPtr]::Zero) {
        return $false
    }

    $icon = [System.Drawing.Icon]::FromHandle($shinfo.hIcon)
    try {
        $bmp = $icon.ToBitmap()
        Save-LetterboxedIcon16 -Image $bmp -Path $TargetPath
        $bmp.Dispose()
        Write-Host "Captured $Extension file-type icon -> $TargetPath"
        return $true
    }
    finally {
        $icon.Dispose()
    }
}

function Save-ExeIcon16 {
    param(
        [string[]]$ExeCandidates,
        [string]$TargetPath
    )
    foreach ($pattern in $ExeCandidates) {
        $exe = Get-Item $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $exe) { continue }
        $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe.FullName)
        if (-not $icon) { continue }
        $bmp = $icon.ToBitmap()
        Save-LetterboxedIcon16 -Image $bmp -Path $TargetPath
        $bmp.Dispose()
        $icon.Dispose()
        Write-Host "Extracted app icon from $($exe.FullName) -> $TargetPath"
        return $true
    }
    return $false
}

$framework = "Client\Explorer\Framework\Resources"
$sdkForms = "Client\SDK\Autodesk.DataManagement.Client.Framework.Vault.Forms\Resources"

$depotByTheme = @{}
foreach ($theme in @("Light", "Dark")) {
    $depotByTheme[$theme] = @{
        assignupdateitem = (Copy-DepotIcon "$framework\$theme\UpdateItem_16.png" $theme "assignupdateitem.png")
        createpdf        = (Copy-DepotIcon "$framework\$theme\CreatePDF_16.png" $theme "createpdf.png")
        dwf              = (Copy-DepotIcon "$framework\$theme\dwf_16.png" $theme "dwf.png")
        export           = (Copy-DepotIcon "$framework\$theme\Export_16.png" $theme "export.png")
        mosaic           = (Copy-DepotIcon "$framework\$theme\mosaic_16.png" $theme "mosaic.png")
        thumbnail        = (Copy-DepotIcon "$sdkForms\$theme\thumbnail_16.png" $theme "thumbnail.png")
    }
}

# Preserve curated application icons (not used on menus).
$appTargets = @{
    inventor   = @()
    revit      = @("C:\Program Files\Autodesk\Revit *\Revit.exe")
    navisworks = @(
        "C:\Program Files\Autodesk\Navisworks Manage *\Roamer.exe",
        "C:\Program Files\Autodesk\Navisworks *\Roamer.exe"
    )
    solidworks = @(
        "C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe",
        "C:\Program Files\Dassault Systemes\SOLIDWORKS*\SLDWORKS.exe"
    )
    autocad    = @("C:\Program Files\Autodesk\AutoCAD *\acad.exe")
    word       = @(
        "C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE",
        "C:\Program Files (x86)\Microsoft Office\root\Office16\WINWORD.EXE"
    )
}

foreach ($entry in $appTargets.GetEnumerator()) {
    $path = Join-Path $sharedApps "$($entry.Key).png"
    if (Test-Path $path) {
        Write-Host "Keeping curated app icon: $path"
        continue
    }
    if ($entry.Value.Count -lt 1) {
        continue
    }
    if (-not (Save-ExeIcon16 -ExeCandidates $entry.Value -TargetPath $path)) {
        Write-Warning "No EXE icon for $($entry.Key); add Resources/Icons/_apps/$($entry.Key).png manually if needed."
    }
}

# Capture file-type icons when missing (run on a workstation with registered extensions).
$formatExtensions = @{
    png = ".png"
    pdf = ".pdf"
    nwd = ".nwd"
    rvt = ".rvt"
    dwg = ".dwg"
    jt  = ".jt"
}

foreach ($entry in $formatExtensions.GetEnumerator()) {
    $target = Join-Path $sharedFormats "$($entry.Key).png"
    if (Test-Path $target) {
        Write-Host "Keeping curated format icon: $target"
        continue
    }
    if (-not (Export-ExtensionIcon16 -Extension $entry.Value -TargetPath $target)) {
        Write-Warning "Could not capture $($entry.Value) icon; add $target manually."
    }
}

function Resolve-FormatSource {
    param([string]$FormatKey, [string]$DepotFallbackKey, [string]$Theme)
    $formatPath = Join-Path $sharedFormats "$FormatKey.png"
    if (Test-Path $formatPath) {
        return $formatPath
    }
    if ($DepotFallbackKey -and $depotByTheme[$Theme][$DepotFallbackKey]) {
        return $depotByTheme[$Theme][$DepotFallbackKey]
    }
    return $null
}

# MenuKey -> FormatKey in _formats, optional depot fallback key per theme
$menuIconMap = @(
    @{ MenuKey = "assignupdateitem"; FormatKey = $null; DepotKey = "assignupdateitem" }
    @{ MenuKey = "inventor_png"; FormatKey = "png"; DepotKey = "thumbnail" }
    @{ MenuKey = "nwd"; FormatKey = "nwd"; DepotKey = "mosaic" }
    @{ MenuKey = "pdf_slddrw"; FormatKey = "pdf"; DepotKey = "createpdf" }
    @{ MenuKey = "pdf_office"; FormatKey = "pdf"; DepotKey = "createpdf" }
    @{ MenuKey = "rvt"; FormatKey = "rvt"; DepotKey = $null }
    @{ MenuKey = "dwg_2d"; FormatKey = "dwg"; DepotKey = $null }
    @{ MenuKey = "export3d_formats"; FormatKey = "dwg"; DepotKey = "export" }
)

foreach ($theme in @("Light", "Dark")) {
    foreach ($map in $menuIconMap) {
        $source = $null
        if ($map.FormatKey) {
            $source = Resolve-FormatSource -FormatKey $map.FormatKey -DepotFallbackKey $map.DepotKey -Theme $theme
        }
        elseif ($map.DepotKey) {
            $source = $depotByTheme[$theme][$map.DepotKey]
        }

        if (-not $source) {
            Write-Warning "No icon source for menu '$($map.MenuKey)' ($theme)"
            continue
        }

        if (-not (Write-MenuIconFromSource -SourcePath $source -Theme $theme -MenuKey $map.MenuKey)) {
            Write-Warning "Failed to write menu icon $($map.MenuKey) ($theme)"
        }
    }
}

Write-Host "Menu icons written to $outRoot\{Light,Dark}"
Write-Host "Format sources: $sharedFormats"
Write-Host "App icons (reference): $sharedApps"
