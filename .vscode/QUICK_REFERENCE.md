# VS Code Quick Reference

Quick keyboard shortcuts and commands for developing AvantiPoint Packages in Visual Studio Code.

## Building

| Action | Windows/Linux | macOS | Menu |
|--------|---------------|-------|------|
| Build solution (default) | `Ctrl+Shift+B` | `Cmd+Shift+B` | Terminal > Run Build Task |
| Run any task | `Ctrl+Shift+P` → Tasks: Run Task | `Cmd+Shift+P` → Tasks: Run Task | Terminal > Run Task |

## Running & Debugging

| Action | Windows/Linux | macOS | Menu |
|--------|---------------|-------|------|
| Start debugging | `F5` | `F5` | Run > Start Debugging |
| Run without debugging | `Ctrl+F5` | `Cmd+F5` | Run > Run Without Debugging |
| Stop debugging | `Shift+F5` | `Shift+F5` | Run > Stop Debugging |
| Restart debugging | `Ctrl+Shift+F5` | `Cmd+Shift+F5` | Run > Restart Debugging |
| Open Run view | `Ctrl+Shift+D` | `Cmd+Shift+D` | View > Run |

## Testing

| Action | Windows/Linux | macOS | Command Palette |
|--------|---------------|-------|-----------------|
| Run all tests | Terminal > Run Task > test | Terminal > Run Task > test | Run Test Task |
| Run tests with coverage | Terminal > Run Task > test with coverage | Terminal > Run Task > test with coverage | - |

## Debugging

| Action | Windows/Linux | macOS |
|--------|---------------|-------|
| Toggle breakpoint | `F9` | `F9` |
| Step over | `F10` | `F10` |
| Step into | `F11` | `F11` |
| Step out | `Shift+F11` | `Shift+F11` |
| Continue | `F5` | `F5` |
| Conditional breakpoint | Right-click margin | Right-click margin |

## Code Navigation

| Action | Windows/Linux | macOS |
|--------|---------------|-------|
| Go to definition | `F12` | `F12` |
| Peek definition | `Alt+F12` | `Option+F12` |
| Go to implementation | `Ctrl+F12` | `Cmd+F12` |
| Find all references | `Shift+F12` | `Shift+F12` |
| Go to symbol | `Ctrl+Shift+O` | `Cmd+Shift+O` |
| Go to file | `Ctrl+P` | `Cmd+P` |
| Command palette | `Ctrl+Shift+P` | `Cmd+Shift+P` |

## Editing

| Action | Windows/Linux | macOS |
|--------|---------------|-------|
| Format document | `Shift+Alt+F` | `Shift+Option+F` |
| Organize imports | Save file | Save file |
| Rename symbol | `F2` | `F2` |
| Quick fix | `Ctrl+.` | `Cmd+.` |
| Comment/uncomment | `Ctrl+/` | `Cmd+/` |
| Multi-cursor | `Alt+Click` | `Option+Click` |

## Terminal

| Action | Windows/Linux | macOS |
|--------|---------------|-------|
| Toggle terminal | `` Ctrl+` `` | `` Cmd+` `` |
| New terminal | `Ctrl+Shift+` ` | `Cmd+Shift+` ` |
| Split terminal | `Ctrl+Shift+5` | `Cmd+Shift+5` |

## Launch Configurations

### Available Configurations
1. **Launch OpenFeed** - Port 5000/5001, with seeder
2. **Launch AuthenticatedFeed** - Port 5002/5003, with seeder  
3. **Launch OpenFeed (No Seeder)** - Port 5000/5001, seeder disabled
4. **Launch Both Feeds** - Both feeds simultaneously
5. **Attach to OpenFeed** - Attach debugger to running process
6. **Attach to AuthenticatedFeed** - Attach debugger to running process

### Switching Configurations
Press `F5` or click the dropdown in the Run view to select a configuration.

## Common Tasks

### Start OpenFeed for Development
1. Press `F5`
2. Select "Launch OpenFeed"
3. Browser opens automatically at https://localhost:5001

### Run Both Feeds Simultaneously
1. Press `F5`
2. Select "Launch Both Feeds"
3. OpenFeed: https://localhost:5001
4. AuthenticatedFeed: https://localhost:5003

### Run Tests
1. Press `Ctrl+Shift+P` (or `Cmd+Shift+P`)
2. Type "Run Task"
3. Select "test"

### Watch Mode (Auto-rebuild)
1. Open Command Palette (`Ctrl+Shift+P`)
2. Type "Run Task"
3. Select "watch OpenFeed" or "watch AuthenticatedFeed"
4. Make code changes - app rebuilds and restarts automatically

### Clean Build
1. Run task: "clean"
2. Run task: "restore"
3. Run task: "build"

## Tips

- **IntelliSense**: Type `Ctrl+Space` to trigger IntelliSense suggestions
- **Parameter hints**: Type `Ctrl+Shift+Space` while in method parameters
- **Problems panel**: `Ctrl+Shift+M` to see build errors and warnings
- **Search files**: `Ctrl+P` and start typing filename
- **Search in files**: `Ctrl+Shift+F` to search across all files
- **Reload window**: `Ctrl+Shift+P` → "Developer: Reload Window" if extensions misbehave

## Environment Variables

Configurations set these variables automatically:

### OpenFeed
```
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5000;https://localhost:5001
```

### OpenFeed (No Seeder)
```
ASPNETCORE_ENVIRONMENT=Testing
ASPNETCORE_URLS=http://localhost:5000;https://localhost:5001
SampleDataSeeder__Enabled=false
```

### AuthenticatedFeed
```
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5002;https://localhost:5003
```

## Troubleshooting

### Port Already in Use
Change ports in `.vscode/launch.json` under the `env.ASPNETCORE_URLS` property.

### Build Fails
1. Check Problems panel (`Ctrl+Shift+M`)
2. Run "restore" task
3. Run "clean" task, then "build"

### Debugger Won't Start
1. Ensure C# Dev Kit extension is installed
2. Check that .NET 10.0 SDK is installed
3. Reload window: `Ctrl+Shift+P` → "Developer: Reload Window"

### IntelliSense Not Working
1. Open Command Palette: `Ctrl+Shift+P`
2. Run ".NET: Restart Language Server"
3. If still broken, reload window

## Learn More

- [VS Code Keyboard Shortcuts (PDF)](https://code.visualstudio.com/shortcuts/keyboard-shortcuts-windows.pdf)
- [C# in VS Code](https://code.visualstudio.com/docs/languages/csharp)
- [Debugging in VS Code](https://code.visualstudio.com/docs/editor/debugging)
