# UtilityBelt Development Commands

## Build Commands
```bash
# Build entire solution (Release configuration)
dotnet build -c Release

# Build specific project
dotnet build UtilityBelt/UtilityBelt.csproj -c Release

# Build with specific version (used in CI)
dotnet build -c Release -p:PackageReleaseNotes="changelog content"
```

## Development Workflow
```bash
# Build solution from project root
dotnet build UtilityBelt.sln -c Release

# Clean build artifacts
rm -rf bin/ UtilityBelt/obj/ UBLoader/obj/
rm -f UtilityBelt/Properties/AssemblyInfo.cs UBLoader/Properties/AssemblyInfo.cs

# Manual installer creation (Windows)
cd UtilityBelt
makensis installer.nsi
```

## Git Commands (macOS/Darwin)
```bash
# Standard git operations
git status
git add .
git commit -m "message"
git push origin master

# Branch management
git checkout -b feature-branch
git merge feature-branch
```

## File Operations (macOS/Darwin)
```bash
# Directory listing
ls -la

# File search
find . -name "*.cs" -type f
grep -r "pattern" --include="*.cs" .

# File operations
cp source dest
mv source dest
rm file
```

## Project-Specific Notes
- **Target Platform**: x86 (32-bit) only
- **Output Directory**: `bin/Release/`
- **Installer Output**: `bin/Release/UtilityBelt-Installer-{version}.exe`
- **Dependencies**: Located in `deps/` directory
- **No Traditional Testing**: No unit test framework detected
- **CI/CD**: GitLab CI with Docker-based builds