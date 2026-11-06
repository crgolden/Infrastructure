param([string]$Goal, [string]$Probe, [string]$Only)

$ErrorActionPreference = 'Continue'
$gateCommon = $env:GATE_LIBRARY
if (-not $gateCommon -or -not (Test-Path -LiteralPath $gateCommon -PathType Leaf)) {
    Write-Host 'GATE: FAILED (GATE_LIBRARY names no gate library script; README.md, section Gate, says how to plug one in or run the steps without one)'
    exit 1
}
. $gateCommon
$gateOutput = if (Get-Command Get-GateOutputDirectory -ErrorAction SilentlyContinue) {
    Get-GateOutputDirectory (Split-Path -Leaf $PSScriptRoot)
} else {
    (New-Item -ItemType Directory -Force -Path (Join-Path ([IO.Path]::GetTempPath()) "claude\crgolden-gates\$(Split-Path -Leaf $PSScriptRoot)")).FullName
}
Register-GateSteps @('Restore local tools', 'Begin Sonar analysis', 'Build with dotnet', 'jb inspectcode',
    'Run unit tests with coverage', 'End Sonar analysis',
    'Fail on open Sonar issues')
Register-StepInputs @{
    'Restore local tools'          = @('dotnet-tools.json')
    'Begin Sonar analysis'         = @('*')
    'Build with dotnet'            = @('*')
    'jb inspectcode'               = @('*')
    'Run unit tests with coverage' = @('*')
    'End Sonar analysis'           = @('*')
    'Fail on open Sonar issues'    = @('*')
}
$repo = $PSScriptRoot
$sarif = (Join-Path $gateOutput 'infrastructure-inspect.sarif')
$unitTrx = Join-Path $repo 'Infrastructure.Tests.Unit\bin\Release\net10.0\TestResults\unit-tests.trx'
$sonarBranch = Get-SonarBranchName
$beginSonar = "Begin Sonar analysis (branch $sonarBranch)"
$build = 'Build with dotnet (Release, RestoreLockedMode)'
$endSonar = 'End Sonar analysis (quality gate waited)'
$sonarIssues = 'Fail on open Sonar issues'
$unit = 'Run unit tests with coverage (Category=Unit)'
$env:TZ = 'UTC'
if ($env:TZ -ne 'UTC') { Write-Host 'GATE: FAILED (TZ pin)'; exit 1 }
Set-Location $repo
$probeProjects = [ordered]@{ 'Run unit tests with coverage' = 'Infrastructure.Tests.Unit' }
if ($Only -and -not $Probe) { Write-Host 'PROBE: FAILED (-Only names the one test a probe runs, so it needs -Probe)'; exit 1 }
if ($Probe -and (-not $probeProjects.Contains($Probe.Trim()) -or [string]::IsNullOrWhiteSpace($Only) -or $Only.Contains(','))) {
    Write-Host "PROBE: FAILED (a probe runs exactly one test: -Probe '<$($probeProjects.Keys -join ' | ')>' -Only '<fully qualified test method>')"
    exit 1
}
if ($Probe) {
    $probeStep = $Probe.Trim()
    $probeResults = Join-Path $gateOutput 'probe-results'
    $probeTrx = Join-Path $probeResults 'probe.trx'
    if (Test-Path -LiteralPath $probeTrx) { Remove-Item -LiteralPath $probeTrx -Force }
    Write-Host "PROBE: running one test, '$Only' of $probeStep; no gate record or verdict is written"
    $global:LASTEXITCODE = $null
    dotnet test --project $probeProjects[$probeStep] --no-build --configuration Release -- --filter-method $Only --report-xunit-trx --report-xunit-trx-filename probe.trx --results-directory=$probeResults
    $probeExit = $global:LASTEXITCODE
    if (-not (Test-Path -LiteralPath $probeTrx)) { Write-Host "PROBE: FAILED (no TRX, exit $probeExit)"; exit 1 }
    [xml]$probeDoc = Get-Content -LiteralPath $probeTrx -Raw
    $counters = $probeDoc.TestRun.ResultSummary.Counters
    if ($probeExit -ne 0 -or [int]$counters.executed -ne 1 -or [int]$counters.failed -ne 0) {
        Write-Host "PROBE: FAILED (a probe runs one test, and '$Only' executed $($counters.executed), failed $($counters.failed), exit $probeExit)"
        exit 1
    }
    Write-Host "PROBE: PASSED for the one test named, against the last build's binaries; no verdict was saved"
    exit 0
}
Initialize-GateState 'Infrastructure' $repo
Resolve-OwedSteps
Invoke-CatalogSteps

$global:LASTEXITCODE = $null
dotnet tool restore
$null = Test-Exit 'Restore local tools (dotnet tool restore)'

$sonarCarried = Test-StepCarried $sonarIssues
if ($sonarCarried) {
    $null = Test-StepCarried $beginSonar
    $null = Test-StepCarried $build
    $null = Test-StepCarried $endSonar
}
else {
    $sonarStartedAt = [DateTimeOffset]::UtcNow
    $env:JAVA_HOME = Get-SonarScannerJavaHome $beginSonar
    $global:LASTEXITCODE = $null
    dotnet-sonarscanner begin /k:"crgolden_Infrastructure" /o:"crgolden" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.cs.opencover.reportsPaths="coverage.opencover.xml" /d:sonar.exclusions="**/bin/**,**/obj/**" /d:sonar.coverage.exclusions="**/Program.cs,**/gate.ps1" /d:sonar.qualitygate.wait=true /d:sonar.scanner.skipJreProvisioning=true /d:sonar.branch.name="$sonarBranch"
    $null = Test-Exit $beginSonar

    $global:LASTEXITCODE = $null
    dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
    $null = Test-Exit $build
}

if (-not (Test-StepCarried 'jb inspectcode')) {
    if (Test-Path $sarif) { Remove-Item $sarif -Force }
    dotnet jb inspectcode "$repo\Infrastructure.slnx" --no-build -e=WARNING --caches-home="$(New-InspectCodeCaches $gateOutput)" --output="$sarif"
    Test-Sarif $sarif
}

if (-not (Test-StepCarried $unit)) {
    if (Test-Path $unitTrx) { Remove-Item $unitTrx -Force }
    $global:LASTEXITCODE = $null
    dotnet coverlet Infrastructure.Tests.Unit\bin\Release\net10.0 `
        --target "dotnet" `
        --targetargs "test --project Infrastructure.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --stop-on-fail on --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Infrastructure.Tests.Unit/bin/Release/net10.0/TestResults" `
        --format opencover --output "coverage.opencover.xml" `
        --skipautoprops --exclude-by-attribute GeneratedCodeAttribute --exclude-by-file "**/obj/**" `
        --exclude-by-file "**/Program.cs" --does-not-return-attribute DoesNotReturnAttribute --include "[Infrastructure]*"
    Test-Trx $unit $unitTrx $global:LASTEXITCODE -floor 1
}

if (-not $sonarCarried) {
    $global:LASTEXITCODE = $null
    dotnet-sonarscanner end
    $null = Test-Exit $endSonar
    Test-SonarIssues $sonarIssues 'crgolden_Infrastructure' $sonarBranch $sonarStartedAt
}

Write-Row 'Upload test results / dotnet publish / Upload artifact / deploy' 'NOT RUN' 'delivery steps, not checks'
Complete-Gate
