# Build Installer Script for Rewrite Assistant
# This script automates the entire build and packaging process

param(
    [string]$Version = "1.0.0",
    [switch]$SkipClean = $false
)

$ErrorActionPreference = "Stop"

# Configuration
$ProjectRoot = $PSScriptRoot
$BackendDir = Join-Path $ProjectRoot "src\backend"
$FrontendProject = Join-Path $ProjectRoot "src\RewriteAssistant\RewriteAssistant.csproj"
$StagingDir = Join-Path $ProjectRoot "staging"
$OutputDir = Join-Path $ProjectRoot "output"
$InstallerScript = Join-Path $ProjectRoot "installer.iss"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Rewrite Assistant Installer Build Script" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to check if a command exists
function Test-Command {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Function to display error and exit
function Exit-WithError {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

# Step 1: Check Prerequisites
Write-Host "[1/7] Checking prerequisites..." -ForegroundColor Yellow

# Check .NET SDK
if (-not (Test-Command "dotnet")) {
    Exit-WithError ".NET SDK not found. Please install from https://dotnet.microsoft.com/download"
}
$dotnetVersion = dotnet --version
Write-Host "  [OK] .NET SDK found: $dotnetVersion" -ForegroundColor Green

# Check Node.js and npm
if (-not (Test-Command "node")) {
    Exit-WithError "Node.js not found. Please install from https://nodejs.org/"
}
$nodeVersion = node --version
Write-Host "  [OK] Node.js found: $nodeVersion" -ForegroundColor Green

if (-not (Test-Command "npm")) {
    Exit-WithError "npm not found. Please install Node.js from https://nodejs.org/"
}
$npmVersion = npm --version
Write-Host "  [OK] npm found: $npmVersion" -ForegroundColor Green

# Check pkg (install if missing)
if (-not (Test-Command "pkg")) {
    Write-Host "  [WARN] pkg not found. Installing globally..." -ForegroundColor Yellow
    npm install -g pkg
    if (-not (Test-Command "pkg")) {
        Exit-WithError "Failed to install pkg. Please run 'npm install -g pkg' manually."
    }
}
Write-Host "  [OK] pkg found" -ForegroundColor Green

# Check Inno Setup
$InnoSetupPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
)

$InnoSetupCompiler = $null
foreach ($path in $InnoSetupPaths) {
    if (Test-Path $path) {
        $InnoSetupCompiler = $path
        break
    }
}

if (-not $InnoSetupCompiler) {
    Exit-WithError "Inno Setup not found. Please install from https://jrsoftware.org/isdl.php"
}
Write-Host "  [OK] Inno Setup found: $InnoSetupCompiler" -ForegroundColor Green

Write-Host ""

# Step 2: Clean previous build artifacts
if (-not $SkipClean) {
    Write-Host "[2/7] Cleaning previous build artifacts..." -ForegroundColor Yellow
    
    if (Test-Path $StagingDir) {
        Remove-Item -Path $StagingDir -Recurse -Force
        Write-Host "  [OK] Removed staging directory" -ForegroundColor Green
    }
    
    if (Test-Path $OutputDir) {
        Remove-Item -Path $OutputDir -Recurse -Force
        Write-Host "  [OK] Removed output directory" -ForegroundColor Green
    }
    
    $backendDist = Join-Path $BackendDir "dist"
    if (Test-Path $backendDist) {
        Remove-Item -Path $backendDist -Recurse -Force
        Write-Host "  [OK] Removed backend dist directory" -ForegroundColor Green
    }
} else {
    Write-Host "[2/7] Skipping clean (--SkipClean flag set)" -ForegroundColor Yellow
}

Write-Host ""

# Step 3: Build Node.js backend
Write-Host "[3/7] Building Node.js backend..." -ForegroundColor Yellow

Push-Location $BackendDir
try {
    # Install dependencies if needed
    if (-not (Test-Path "node_modules")) {
        Write-Host "  Installing backend dependencies..." -ForegroundColor Cyan
        npm install
    }
    
    # Build TypeScript to JavaScript
    Write-Host "  Compiling TypeScript..." -ForegroundColor Cyan
    npm run build
    
    if (-not (Test-Path "dist\index.js")) {
        Exit-WithError "Backend build failed - dist\index.js not found"
    }
    
    Write-Host "  [OK] Backend built successfully" -ForegroundColor Green
} finally {
    Pop-Location
}

Write-Host ""

# Step 4: Package backend with pkg
Write-Host "[4/7] Packaging backend with pkg..." -ForegroundColor Yellow

$backendEntry = Join-Path $BackendDir "dist\index.js"
$backendOutput = Join-Path $StagingDir "backend.exe"

# Create staging directory
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

Write-Host "  Creating standalone backend.exe..." -ForegroundColor Cyan
pkg $backendEntry --target node18-win-x64 --output $backendOutput

if (-not (Test-Path $backendOutput)) {
    Exit-WithError "pkg failed to create backend.exe"
}

$backendSize = [math]::Round((Get-Item $backendOutput).Length / 1MB, 2)
Write-Host "  [OK] backend.exe created ($backendSize MB)" -ForegroundColor Green

Write-Host ""

# Step 5: Publish WPF frontend
Write-Host "[5/7] Publishing WPF frontend..." -ForegroundColor Yellow

$frontendOutput = Join-Path $StagingDir "RewriteAssistant.exe"

Write-Host "  Publishing self-contained single-file executable..." -ForegroundColor Cyan
dotnet publish $FrontendProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $StagingDir

if (-not (Test-Path $frontendOutput)) {
    Exit-WithError "Frontend publish failed - RewriteAssistant.exe not found"
}

$frontendSize = [math]::Round((Get-Item $frontendOutput).Length / 1MB, 2)
Write-Host "  [OK] RewriteAssistant.exe created ($frontendSize MB)" -ForegroundColor Green

Write-Host ""

# Step 6: Verify staging directory
Write-Host "[6/7] Verifying staged files..." -ForegroundColor Yellow

$requiredFiles = @("RewriteAssistant.exe", "backend.exe")
$allFilesPresent = $true

foreach ($file in $requiredFiles) {
    $filePath = Join-Path $StagingDir $file
    if (Test-Path $filePath) {
        $size = [math]::Round((Get-Item $filePath).Length / 1MB, 2)
        Write-Host "  [OK] $file ($size MB)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] $file (missing)" -ForegroundColor Red
        $allFilesPresent = $false
    }
}

if (-not $allFilesPresent) {
    Exit-WithError "Not all required files are present in staging directory"
}

Write-Host ""

# Step 7: Run Inno Setup compiler
Write-Host "[7/7] Creating installer with Inno Setup..." -ForegroundColor Yellow

# Create output directory
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "  Running Inno Setup compiler..." -ForegroundColor Cyan
& $InnoSetupCompiler $InstallerScript

$installerPath = Join-Path $OutputDir "RewriteAssistantSetup.exe"
if (-not (Test-Path $installerPath)) {
    Exit-WithError "Inno Setup failed to create installer"
}

$installerSize = [math]::Round((Get-Item $installerPath).Length / 1MB, 2)
Write-Host "  [OK] Installer created: $installerPath ($installerSize MB)" -ForegroundColor Green

# Check size requirement (under 200MB)
if ($installerSize -gt 200) {
    Write-Host "  [WARN] Warning: Installer size ($installerSize MB) exceeds 200MB target" -ForegroundColor Yellow
} else {
    Write-Host "  [OK] Installer size is within 200MB requirement" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installer location: $installerPath" -ForegroundColor Cyan
Write-Host "Installer size: $installerSize MB" -ForegroundColor Cyan
Write-Host ""
