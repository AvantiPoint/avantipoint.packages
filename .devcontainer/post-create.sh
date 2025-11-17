#!/bin/bash

set -e

echo "Setting up AvantiPoint Packages development environment..."

# Install Python dependencies for documentation
echo "Installing Python dependencies for MkDocs..."
pip install --upgrade pip
pip install -r .github/workflows/requirements.txt

# Install .NET EF Core tools globally (for database migrations)
echo "Installing Entity Framework Core tools..."
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef

# Add .NET tools to PATH
export PATH="$PATH:/root/.dotnet/tools"

# Restore NuGet packages
echo "Restoring NuGet packages..."
dotnet restore APPackages.sln

# Build the solution to verify everything is working
echo "Building solution..."
dotnet build APPackages.sln --no-restore

echo ""
echo "✅ Development environment setup complete!"
echo ""
echo "You can now:"
echo "  - Run 'dotnet build' to build the solution"
echo "  - Run 'dotnet test' to run tests"
echo "  - Run sample apps from samples/OpenFeed or samples/AuthenticatedFeed"
echo "  - Run 'mkdocs serve' to preview documentation locally"
echo "  - Run 'dotnet ef' commands for database migrations"
echo ""
