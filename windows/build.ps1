param([switch]$Test, [switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot 'dist'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework 4.x C# compiler is required.' }
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$executablePath = Join-Path $outputDirectory 'PixelTxtConverter.exe'
$sourcePaths = @('Core.cs', 'WebpDecoder.cs', 'TxpMonitor.cs', 'ShellIntegration.cs', 'Program.cs', 'AssemblyInfo.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$decoderPath = Join-Path $PSScriptRoot 'vendor\webp\dwebp.exe'
if ((Get-FileHash -LiteralPath $decoderPath -Algorithm SHA256).Hash -ne '17C1488BF84B7834E9AA908BB40AFD0BE2E55D57567D210E96336C56BB6AE993') { throw 'Bundled WebP decoder checksum mismatch.' }
$resources = @("/resource:$decoderPath,PixelTxt.dwebp.exe", "/resource:$(Join-Path $PSScriptRoot 'vendor\webp\COPYING'),PixelTxt.WebpLicense", "/resource:$(Join-Path $PSScriptRoot 'vendor\webp\PATENTS'),PixelTxt.WebpPatents")
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ "/out:$executablePath" "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $resources $sourcePaths
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built: $executablePath"
if ($Test) {
    $testExecutable = Join-Path $outputDirectory 'WindowsTests.exe'
    & $compilerPath /nologo /target:exe "/out:$testExecutable" "/reference:$executablePath" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Windows tests failed.' }
}
if ($Package) {
    $packagedExecutable = Join-Path $projectRoot 'PixelTxtConverter.exe'
    Copy-Item -LiteralPath $executablePath -Destination $packagedExecutable -Force
    $checksum = (Get-FileHash -LiteralPath $packagedExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath (Join-Path $projectRoot 'SHA256SUMS.txt') -Value "$checksum  PixelTxtConverter.exe" -Encoding ascii
    Write-Output "Packaged: $packagedExecutable"
}
