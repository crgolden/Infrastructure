@{
    Schema   = 1
    Repo     = 'Infrastructure'
    Workflow = '.github/workflows/main_crgolden-infrastructure.yml'
    Sonar    = @{ ProjectKey = 'crgolden_Infrastructure' }
    Probe    = @{ Kind = 'dotnet'; Projects = @('Infrastructure.Tests.Unit') }
    Steps    = @(
        @{ Name = 'Regenerate lock files and push'; Delivery = $true }
        @{ Name = 'Install SonarQube Cloud scanner'; Tool = 'dotnet-sonarscanner'; Inputs = @('*') }
        @{ Name = 'Restore local tools'; Run = @('dotnet', 'tool', 'restore'); Inputs = @('dotnet-tools.json'); Always = $true }
        @{
            Name   = 'Begin Sonar analysis'
            Sonar  = 'begin'
            Needs  = @('Install SonarQube Cloud scanner', 'Restore local tools')
            Inputs = @('*')
            Run    = @(
                'dotnet-sonarscanner', 'begin', '/k:{sonarProject}', '/o:crgolden', '/d:sonar.host.url=https://sonarcloud.io',
                '/d:sonar.cs.opencover.reportsPaths=coverage.opencover.xml',
                '/d:sonar.exclusions=**/bin/**,**/obj/**', '/d:sonar.coverage.exclusions=**/Program.cs,**/gate.ps1,**/gate.steps.psd1',
                '/d:sonar.qualitygate.wait=true', '/d:sonar.scanner.skipJreProvisioning=true',
                '/d:sonar.scm.use.blame.algorithm=GIT_NATIVE_BLAME', '/d:sonar.branch.name={sonarBranch}'
            )
        }
        @{
            Name   = 'Build with dotnet'
            Sonar  = 'build'
            Needs  = @('Restore local tools')
            Inputs = @('*')
            Run    = @('dotnet', 'build', '--no-incremental', '--configuration', 'Release', '/p:RestoreLockedMode=true', '-warnaserror')
        }
        @{
            Name   = 'ReSharper inspection (jb inspectcode)'
            Needs  = @('Build with dotnet')
            Inputs = @('*')
            Remove = @('{output}\infrastructure-inspect.sarif')
            Run    = @('dotnet', 'jb', 'inspectcode', 'Infrastructure.slnx', '--no-build', '-e=WARNING', '--caches-home={inspectCache}', '--output={output}\infrastructure-inspect.sarif')
            Check  = @{ Sarif = '{output}\infrastructure-inspect.sarif' }
        }
        @{
            Name   = 'Run unit tests with coverage'
            Needs  = @('Build with dotnet')
            Inputs = @('*')
            Remove = @('Infrastructure.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx')
            Run    = @(
                'dotnet', 'coverlet', 'Infrastructure.Tests.Unit/bin/Release/net11.0', '--target', 'dotnet',
                '--targetargs', 'test --project Infrastructure.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Infrastructure.Tests.Unit/bin/Release/net11.0/TestResults',
                '--format', 'opencover', '--output', 'coverage.opencover.xml', '--skipautoprops',
                '--exclude-by-attribute', 'GeneratedCodeAttribute', '--exclude-by-file', '**/obj/**', '--exclude-by-file', '**/Program.cs',
                '--does-not-return-attribute', 'DoesNotReturnAttribute', '--include', '[Infrastructure]*'
            )
            Check  = @{ Trx = 'Infrastructure.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx'; Floor = 1 }
        }
        @{ Name = 'dotnet publish'; Delivery = $true }
        @{
            Name   = 'End Sonar analysis'
            Sonar  = 'end'
            Needs  = @('Begin Sonar analysis', 'Build with dotnet')
            Inputs = @('*')
            Run    = @('dotnet-sonarscanner', 'end')
        }
        @{ Name = 'Fail on open Sonar issues'; Sonar = 'issues'; Needs = @('End Sonar analysis'); Inputs = @('*') }
        @{ Name = 'Run the app on .NET 11'; Delivery = $true }
    )
}
