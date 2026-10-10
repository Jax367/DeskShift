param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
& "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\tools\make-icon.ps1"
if ($LASTEXITCODE -ne 0) { throw '图标生成失败' }
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:assets\DeskShift.ico "/out:$OutputDirectory\DeskShift.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll src\*.cs
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item -Recurse -Force assets "$OutputDirectory\"
Copy-Item -Recurse -Force browser "$OutputDirectory\"
Copy-Item -Force README*.md "$OutputDirectory\"
Copy-Item -Recurse -Force docs "$OutputDirectory\"
Write-Host "已生成 $OutputDirectory\DeskShift.exe"
