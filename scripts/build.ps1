[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [switch] $Restore
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'OperatorTunnel.sln'
$testProject = Join-Path $repoRoot 'OperatorTunnel.Core.Tests\OperatorTunnel.Core.Tests.csproj'
$appProject = Join-Path $repoRoot 'OperatorTunnel.App\OperatorTunnel.App.csproj'

Push-Location $repoRoot
try {
    if ($Restore) {
        dotnet restore $solution
    }

    # Build the Windows app directly. This also builds the referenced Core project
    # and avoids a misleading WPF solution-level status on some SDK installations.
    dotnet build $appProject --configuration $Configuration --no-restore
    dotnet test $testProject --configuration $Configuration --no-restore

    $appBinary = Join-Path $repoRoot "OperatorTunnel.App\bin\$Configuration\net8.0-windows\OperatorTunnel.App.exe"
    if (-not (Test-Path -LiteralPath $appBinary)) {
        throw "Build completed, but the expected application binary was not found: $appBinary"
    }

    Write-Host "`nBuild ready:" -ForegroundColor Green
    Write-Host $appBinary
}
finally {
    Pop-Location
}
