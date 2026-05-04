<#
.SYNOPSIS
    Set Loom's Foundry user-secrets for local development.

.DESCRIPTION
    Wraps `dotnet user-secrets set` for the Loom.Web project, populating
    the Foundry:* configuration block with the team's current Azure
    OpenAI deployment in France Central. Re-running overwrites previous
    values; any missing key prompts for input.

.PARAMETER ApiKey
    The Azure OpenAI API key. If not supplied, the script prompts for
    it as a secure string and never echoes it.

.PARAMETER Endpoint
    The Azure OpenAI resource base URL.
    Default: https://artio-dev-fr-foundry.openai.azure.com

.PARAMETER Deployment
    The deployment name. Default: marketplace-prompt

.PARAMETER ApiVersion
    The Azure OpenAI REST API version. Default: 2024-02-01

.PARAMETER DefaultModel
    Friendly model name reported on Cost.Model. Default: gpt-5.4

.EXAMPLE
    ./scripts/Set-FoundrySecrets.ps1
    # prompts for the API key, applies the team's defaults

.EXAMPLE
    $env:LOOM_FOUNDRY_KEY = '...'
    ./scripts/Set-FoundrySecrets.ps1 -ApiKey $env:LOOM_FOUNDRY_KEY
#>
[CmdletBinding()]
param(
    [string]$ApiKey,
    [string]$Endpoint,
    [string]$Deployment,
    [string]$ApiVersion   = '2024-02-01',
    [string]$DefaultModel = 'gpt-5.4'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'src/Loom.Web/Loom.Web.csproj'

if (-not (Test-Path $project)) {
    throw "Loom.Web project not found at $project. Run this script from a working clone of the Loom repo (the script lives under scripts/)."
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $secure = Read-Host -AsSecureString -Prompt 'Foundry API key'
    $ApiKey = [System.Net.NetworkCredential]::new('', $secure).Password
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw 'API key is required.'
}

function Set-Secret {
    param([Parameter(Mandatory)][string]$Key, [Parameter(Mandatory)][string]$Value)
    & dotnet user-secrets set $Key $Value --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to set $Key (dotnet user-secrets exited $LASTEXITCODE)."
    }
}

Write-Host 'Setting Foundry user-secrets on Loom.Web...' -ForegroundColor Cyan

Set-Secret 'Foundry:Endpoint'     $Endpoint
Set-Secret 'Foundry:Deployment'   $Deployment
Set-Secret 'Foundry:ApiVersion'   $ApiVersion
Set-Secret 'Foundry:DefaultModel' $DefaultModel
Set-Secret 'Foundry:ApiKey'       $ApiKey

Write-Host "Done. Configured $Deployment on $Endpoint (api-version $ApiVersion)." -ForegroundColor Green
Write-Host "Verify with: dotnet user-secrets list --project src/Loom.Web | Select-String '^Foundry:'" -ForegroundColor DarkGray
