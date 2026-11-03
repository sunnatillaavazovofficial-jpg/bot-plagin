<#
  Reklama Maket - saytga qo'yiladigan tayyor paket (sotuvchi uchun).

  Plaginni har bir CorelDRAW versiyasi uchun alohida yig'adi va xaridorga beriladigan ZIP tayyorlaydi:
    dist\ReklamaMaket-setup.zip   (ichida: install.bat, install.ps1, O'QING.txt, v<versiya>\ plagin fayllari)

  Corel.Interop.VGCore.dll quyidagilardan olinadi:
    - shu kompyuterdagi barcha CorelDRAW'lar;
    - interops\<istalgan nom>\Corel.Interop.VGCore.dll (boshqa versiyalar uchun qo'lda qo'yilgan nusxalar).
  Xaridorning CorelDRAW versiyasi uchun yig'ilgan nusxa bo'lmasa, install.bat buni aytadi.

  Oldin: keygen.bat -> "1) Birinchi sozlash" (ochiq kalit plagin kodiga yoziladi).
#>
param(
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Project = Join-Path $Root "src\ReklamaMaket\ReklamaMaket.csproj"
$PublicKeyFile = Join-Path $Root "src\ReklamaMaket\Licensing\LicensePublicKey.cs"
$DistRoot = Join-Path $Root "dist"
$Dist = Join-Path $DistRoot "ReklamaMaket"
$Zip = Join-Path $DistRoot "ReklamaMaket-setup.zip"
$AddonFiles = @("ReklamaMaket.dll", "AppUI.xslt", "UserUI.xslt", "CorelDrw.addon")

function Find-CorelInstalls {
    $bases = @("$env:ProgramFiles\Corel", "$env:ProgramW6432\Corel") |
        Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
    foreach ($base in $bases) {
        Get-ChildItem -Path $base -Filter "CorelDRW.exe" -Recurse -Depth 4 -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.DirectoryName "Assemblies\Corel.Interop.VGCore.dll") } |
            ForEach-Object { $_.DirectoryName }
    }
}

# 1. Faollashtirish yoqilgan bo'lsa, kalit tizimi sozlangan bo'lishi kerak
$LicenseManagerFile = Join-Path $Root "src\ReklamaMaket\Licensing\LicenseManager.cs"
if ((Get-Content $LicenseManagerFile -Raw) -match 'bool Enabled = true;') {
    if ((Get-Content $PublicKeyFile -Raw) -match 'const string X = "";') {
        throw "Kalit tizimi sozlanmagan. Avval keygen.bat ni ishga tushirib, '1) Birinchi sozlash' ni tanlang."
    }
}
else {
    Write-Host "Faollashtirish o'chirilgan: plagin kalitsiz ishlaydi." -ForegroundColor Yellow
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK topilmadi: https://dotnet.microsoft.com/download"
}

# 2. Interop'lar: har bir asosiy versiyadan bittadan
$interops = @()
foreach ($corel in @(Find-CorelInstalls | Select-Object -Unique)) {
    $interops += Join-Path $corel "Assemblies\Corel.Interop.VGCore.dll"
}
$extra = Join-Path $Root "interops"
if (Test-Path $extra) {
    $interops += Get-ChildItem -Path $extra -Filter "Corel.Interop.VGCore.dll" -Recurse | ForEach-Object { $_.FullName }
}
$byVersion = [ordered]@{}
foreach ($dll in $interops) {
    $major = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.Major
    if (-not $byVersion.Contains($major)) {
        $byVersion[$major] = $dll
    }
}
if ($byVersion.Count -eq 0) {
    throw "Corel.Interop.VGCore.dll topilmadi. CorelDRAW o'rnating yoki interops\<nom>\ papkasiga nusxasini qo'ying."
}

# 3. Har bir versiya uchun yig'ish
if (Test-Path $Dist) {
    Remove-Item -Recurse -Force $Dist
}
New-Item -ItemType Directory -Force -Path $Dist | Out-Null
$temp = Join-Path $DistRoot "build"
foreach ($major in $byVersion.Keys) {
    $dll = $byVersion[$major]
    Write-Host ("Yig'ilmoqda: v{0}  ({1})" -f $major, $dll) -ForegroundColor Cyan
    $out = Join-Path $temp "v$major"
    & dotnet build $Project -c Release --no-incremental "-p:VGCorePath=$dll" -o $out
    if ($LASTEXITCODE -ne 0) {
        throw "v$major uchun yig'ishda xato (yuqoridagi xabarlarga qarang)."
    }
    $target = Join-Path $Dist "v$major"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($file in $AddonFiles) {
        Copy-Item -Force -Path (Join-Path $out $file) -Destination $target
    }
}
Remove-Item -Recurse -Force $temp

# 4. O'rnatuvchi va qo'llanma
Copy-Item -Force -Path (Join-Path $Root "installer\*") -Destination $Dist

# 5. ZIP
if (-not $NoZip) {
    if (Test-Path $Zip) {
        Remove-Item -Force $Zip
    }
    Compress-Archive -Path $Dist -DestinationPath $Zip
}

Write-Host ""
Write-Host ("Tayyor: {0} ta CorelDRAW versiyasi uchun ({1})" -f $byVersion.Count, (($byVersion.Keys | ForEach-Object { "v$_" }) -join ", ")) -ForegroundColor Green
if (-not $NoZip) {
    Write-Host "Saytga shu faylni qo'ying: $Zip" -ForegroundColor Green
}
