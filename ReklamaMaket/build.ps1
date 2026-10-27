<#
  Reklama Maket - yig'ish va CorelDRAW'ga o'rnatish.

  Eng oson yo'li: build.bat faylini ikki marta bosing.

  Parametrlar:
    -CorelPath "C:\Program Files\Corel\CorelDRAW Graphics Suite 2021\Programs64"
                 CorelDRAW papkasi (berilmasa avtomatik qidiriladi)
    -NoInstall   faqat yig'ish, Addons papkasiga nusxalamaslik
    -Uninstall   plaginni CorelDRAW'dan olib tashlash
#>
param(
    [string]$CorelPath = "",
    [switch]$NoInstall,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$AddonName = "ReklamaMaket"
$Root = $PSScriptRoot
$ProjectDir = Join-Path $Root "src\ReklamaMaket"
$Project = Join-Path $ProjectDir "ReklamaMaket.csproj"
$OutDir = Join-Path $Root "bin"
$AddonFiles = @("ReklamaMaket.dll", "ReklamaMaket.pdb", "AppUI.xslt", "UserUI.xslt", "CorelDrw.addon")

function Find-CorelInstalls {
    $bases = @("$env:ProgramFiles\Corel", "$env:ProgramW6432\Corel") |
        Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
    foreach ($base in $bases) {
        Get-ChildItem -Path $base -Filter "CorelDRW.exe" -Recurse -Depth 4 -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.DirectoryName "Assemblies\Corel.Interop.VGCore.dll") } |
            ForEach-Object { $_.DirectoryName }
    }
}

function Select-CorelPath {
    $found = @(Find-CorelInstalls | Select-Object -Unique)
    if ($found.Count -eq 0) {
        throw "CorelDRAW topilmadi. Programs64 papkasini qo'lda bering: .\build.ps1 -CorelPath `"...\Programs64`""
    }
    if ($found.Count -eq 1) {
        return $found[0]
    }
    Write-Host "Bir nechta CorelDRAW topildi:"
    for ($i = 0; $i -lt $found.Count; $i++) {
        Write-Host ("  [{0}] {1}" -f ($i + 1), $found[$i])
    }
    $choice = Read-Host "Qaysi biriga o'rnatamiz? Raqamini kiriting"
    return $found[[int]$choice - 1]
}

# PowerShell uchun bitta tirnoqli matn (yo'lda ' bo'lsa ham ishlaydi)
function Quote([string]$text) {
    return "'" + $text.Replace("'", "''") + "'"
}

function Invoke-Elevated([string]$command) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($isAdmin) {
        Invoke-Expression $command
        return
    }
    # Program Files ga yozish uchun administrator huquqi kerak
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    Start-Process powershell -Verb RunAs -Wait -ArgumentList "-NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded"
}

function Wait-CorelClosed {
    while (Get-Process -Name "CorelDRW" -ErrorAction SilentlyContinue) {
        Write-Warning "CorelDRAW ochiq. Uni yoping (aks holda DLL band bo'ladi)."
        Read-Host "CorelDRAW yopilgach Enter bosing" | Out-Null
    }
}

if (-not $CorelPath) {
    $CorelPath = Select-CorelPath
}
$CorelPath = $CorelPath.TrimEnd('\')
if (-not (Test-Path (Join-Path $CorelPath "Assemblies\Corel.Interop.VGCore.dll"))) {
    throw "Bu CorelDRAW papkasi emas (Assemblies\Corel.Interop.VGCore.dll yo'q): $CorelPath"
}
Write-Host "CorelDRAW: $CorelPath" -ForegroundColor Cyan
$Target = Join-Path $CorelPath "Addons\$AddonName"

if ($Uninstall) {
    Wait-CorelClosed
    Invoke-Elevated "if (Test-Path $(Quote $Target)) { Remove-Item -Recurse -Force $(Quote $Target) }"
    Write-Host "Plagin olib tashlandi: $Target" -ForegroundColor Green
    return
}

# Visual Studio ham shu yo'ldan foydalanishi uchun
$props = @"
<Project>
  <PropertyGroup>
    <CorelPath>$CorelPath\</CorelPath>
  </PropertyGroup>
</Project>
"@
Set-Content -Path (Join-Path $ProjectDir "CorelPath.local.props") -Value $props -Encoding UTF8

# ---------------------------------------------------------------- yig'ish
Write-Host "Yig'ilmoqda..." -ForegroundColor Cyan
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    & dotnet build $Project -c Release "-p:CorelPath=$CorelPath" -o $OutDir
}
else {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "Na .NET SDK, na Visual Studio topildi. https://dotnet.microsoft.com/download dan .NET SDK o'rnating."
    }
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    & $msbuild $Project /restore /p:Configuration=Release "/p:CorelPath=$CorelPath" "/p:OutDir=$OutDir/"
}
if ($LASTEXITCODE -ne 0) {
    throw "Yig'ishda xato bo'ldi (yuqoridagi xabarlarga qarang)."
}
Write-Host "Tayyor: $OutDir" -ForegroundColor Green

if ($NoInstall) {
    return
}

# ---------------------------------------------------------------- o'rnatish
Wait-CorelClosed
$copy = "New-Item -ItemType Directory -Force -Path $(Quote $Target) | Out-Null; "
foreach ($file in $AddonFiles) {
    $copy += "Copy-Item -Force -Path $(Quote (Join-Path $OutDir $file)) -Destination $(Quote $Target); "
}
$copy += "Get-ChildItem -Path $(Quote $Target) | Unblock-File"
Invoke-Elevated $copy

if (Test-Path (Join-Path $Target "ReklamaMaket.dll")) {
    Write-Host ""
    Write-Host "O'rnatildi: $Target" -ForegroundColor Green
    Write-Host "CorelDRAW'ni oching: menyuda 'Reklama' > 'Reklama Maket' yoki standart paneldagi tugma."
    Write-Host "Ko'rinmasa: CorelDRAW'ni F8 tugmasini bosib turgan holda ishga tushiring (workspace yangilanadi)."
}
else {
    throw "Nusxalab bo'lmadi: $Target"
}
