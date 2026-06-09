$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $repoRoot "publish"
$projectPath = Join-Path $repoRoot "src/Nagent.Cli/Nagent.Cli.csproj"

Write-Host "Publishing nagent CLI to $publishDir..."

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

$publishDirFull = [System.IO.Path]::GetFullPath($publishDir)
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")

if ([string]::IsNullOrWhiteSpace($userPath)) {
    $newPath = $publishDirFull
}
elseif ($userPath.Split(';') -notcontains $publishDirFull) {
    $newPath = "$userPath;$publishDirFull"
}
else {
    $newPath = $userPath
    Write-Host "Publish directory already on user PATH."
}

if ($newPath -ne $userPath) {
    [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Write-Host "Added $publishDirFull to user PATH."
}

Write-Host "Publish complete: $(Join-Path $publishDir 'nagent.exe')"
