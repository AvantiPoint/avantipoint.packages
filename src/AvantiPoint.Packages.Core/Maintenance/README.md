# Package Maintenance Services

This directory contains background services and utilities for maintaining package metadata and integrity.

## RepositoryCommitBackfillService

A background service that runs on application startup to backfill repository commit metadata for packages that were uploaded before this feature was implemented.

### Features
- Runs automatically on startup (configurable via `EnablePackageMetadataBackfill`)
- Queries packages missing `RepositoryCommit` data but having `RepositoryUrl`
- Extracts commit SHA from package .nuspec files
- Processes packages in batches to avoid system overload
- Saves progress to persistent state file
- Prevents re-processing on subsequent startups

### State Management
The service persists its state to `.metadata/backfill-state.json` in the storage backend. This file tracks:
- Last run time
- Completion status
- Number of packages processed and updated
- Any errors encountered

### Configuration
Enable or disable via `appsettings.json`:
```json
{
  "EnablePackageMetadataBackfill": true
}
```
