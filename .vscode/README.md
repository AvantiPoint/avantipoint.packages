# VS Code Configuration

This directory contains Visual Studio Code workspace configuration for the AvantiPoint Packages project.

## Files

- **tasks.json**: Build, test, and watch tasks
- **launch.json**: Debug configurations for sample projects
- **settings.json**: Workspace settings (formatting, C# configuration, file associations)
- **extensions.json**: Recommended extensions

## Quick Start

### Opening the Workspace

You can open this project in VS Code in two ways:

1. **Open folder**: `File > Open Folder` and select the repository root
2. **Open workspace**: `File > Open Workspace from File` and select `avantipoint.packages.code-workspace`

### Building

- **Build entire solution**: Press `Ctrl+Shift+B` (Windows/Linux) or `Cmd+Shift+B` (Mac)
- **Build specific project**: `Terminal > Run Task` and select the desired build task

Available build tasks:
- `build` - Build entire solution (default)
- `build OpenFeed` - Build OpenFeed sample
- `build AuthenticatedFeed` - Build AuthenticatedFeed sample
- `build SampleDataGenerator` - Build SampleDataGenerator library
- `clean` - Clean all build outputs
- `restore` - Restore NuGet packages

### Running and Debugging

Press `F5` or go to `Run and Debug` view (Ctrl+Shift+D) and select a configuration:

#### Available Configurations

1. **Launch OpenFeed** - Runs OpenFeed on ports 5000 (HTTP) and 5001 (HTTPS)
   - Includes automatic sample data seeding
   - Opens browser automatically when ready

2. **Launch AuthenticatedFeed** - Runs AuthenticatedFeed on ports 5002 (HTTP) and 5003 (HTTPS)
   - Includes automatic sample data seeding
   - Opens browser automatically when ready

3. **Launch OpenFeed (No Seeder)** - Runs OpenFeed without sample data seeding
   - Useful for testing with empty database
   - Environment: `Testing`

4. **Launch Both Feeds** - Compound configuration that runs both feeds simultaneously
   - OpenFeed on 5000/5001
   - AuthenticatedFeed on 5002/5003

5. **Attach to OpenFeed** / **Attach to AuthenticatedFeed** - Attach debugger to running process

### Testing

Run tests using tasks:
- **test** - Run all tests (default test task: `Ctrl+Shift+T`)
- **test with coverage** - Run tests with code coverage collection

### Watch Mode

For rapid development with automatic rebuild on file changes:
- `Terminal > Run Task > watch OpenFeed`
- `Terminal > Run Task > watch AuthenticatedFeed`

## Recommended Extensions

When you open this workspace, VS Code will prompt you to install recommended extensions:

### Essential
- **C# Dev Kit** (`ms-dotnettools.csdevkit`) - Full C# development experience
- **C#** (`ms-dotnettools.csharp`) - C# language support
- **.NET Runtime** (`ms-dotnettools.vscode-dotnet-runtime`) - .NET runtime installer

### Helpful
- **EditorConfig** (`editorconfig.editorconfig`) - EditorConfig support
- **Code Spell Checker** (`streetsidesoftware.code-spell-checker`) - Spelling checker
- **Markdown Lint** (`davidanson.vscode-markdownlint`) - Markdown linting
- **GitHub Pull Requests** (`github.vscode-pull-request-github`) - GitHub integration

## Workspace Settings

The workspace is configured with:

- **Default Solution**: `APPackages.sln`
- **Format on Save**: Enabled for C# files
- **Organize Imports on Save**: Enabled
- **Tab Size**: 4 spaces for C#, 2 for JSON/XML
- **EditorConfig**: Enabled
- **Roslyn Analyzers**: Enabled
- **Hidden Folders**: `bin`, `obj`, `.vs` excluded from file explorer and search

## Debugging Tips

### Breakpoints
- Set breakpoints by clicking in the left margin of the editor
- Conditional breakpoints: Right-click on breakpoint > Edit Breakpoint

### Hot Reload
- Make code changes while debugging
- Changes will be applied automatically (when supported)

### Environment Variables
- Launch configurations include environment variables
- Modify in `launch.json` `env` section

### Multiple Projects
- Use the "Launch Both Feeds" compound configuration to debug both samples simultaneously
- Each will run on different ports to avoid conflicts

## Common Tasks

### Add a New Launch Configuration

1. Open `.vscode/launch.json`
2. Add new configuration to the `configurations` array
3. Specify `preLaunchTask` to build before launch

### Customize Build Tasks

1. Open `.vscode/tasks.json`
2. Add or modify tasks
3. Reference in launch configurations via `preLaunchTask`

### Change Default Build

Modify the task marked with `"isDefault": true` in the `group` section.

## Troubleshooting

### "Build failed" on Launch
- Ensure .NET 10.0 SDK is installed
- Run `dotnet restore` from terminal
- Check build errors in Problems panel

### Debugger Won't Attach
- Ensure the correct process is selected
- Check that the program path in launch.json is correct
- Verify the project has been built in Debug configuration

### Port Already in Use
- Change ports in launch.json `env.ASPNETCORE_URLS`
- Stop any running instances of the applications

### IntelliSense Not Working
- Install C# Dev Kit extension
- Run "Developer: Reload Window" command
- Ensure `APPackages.sln` is set as default solution

## Learn More

- [VS Code C# Documentation](https://code.visualstudio.com/docs/languages/csharp)
- [Debugging in VS Code](https://code.visualstudio.com/docs/editor/debugging)
- [Tasks in VS Code](https://code.visualstudio.com/docs/editor/tasks)
