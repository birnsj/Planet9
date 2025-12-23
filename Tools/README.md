# MonoGame Tools

This directory contains information about MonoGame tools available for this project.

## Installed Tools

The following MonoGame tools are installed as local .NET tools:

1. **MonoGame Content Builder (MGCB)** - Command-line tool for building content
2. **MonoGame Content Builder Editor (MGCB Editor)** - GUI tool for editing .mgcb files

## Usage

### MGCB (Content Builder)

To build content files using the command line:

```bash
dotnet mgcb Content\Content.mgcb
```

Or:

```bash
dotnet tool run mgcb Content\Content.mgcb
```

### MGCB Editor (GUI)

To open the GUI editor for .mgcb files:

```bash
dotnet mgcb-editor
```

Or:

```bash
dotnet tool run mgcb-editor
```

You can also double-click on any `.mgcb` file in Visual Studio or your file explorer, and it should open with the MGCB Editor if it's properly configured.

## Project Content Files

The project's content files are located in:
- `Content\Content.mgcb` - Main content file

## Notes

- Tools are installed locally in `.config/dotnet-tools/` 
- They are available project-wide via `dotnet tool run` or `dotnet <tool-name>`
- To restore tools on a new machine: `dotnet tool restore`

