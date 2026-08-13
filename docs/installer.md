# Installer

This job collection ships as source only in this public repository. A packaged
MSI installer (WiX 4) is built and maintained in a **separate, private repository**
and is not publicly distributed as source.

## Why the installer is private

The installer authoring may reference internal deployment conventions, versioning,
and packaging decisions that are not intended for public distribution alongside
the open-source job implementations.

## Getting the installer

Access to the installer repository and/or a built MSI is granted on request.
Contact the maintainer for details.

## Building your own installer

If you need to package these jobs yourself, each job project already produces a
complete, self-contained `bin\Release\` output (main assembly, `.vcet.config`
registration file, `.settings.xml` configuration, and all copy-local dependencies).
You can author your own WiX (or other) packaging project against these outputs;
each job's Vault Job Processor registration expects the following per job:

- `<job>.dll` — main assembly
- `<job>.vcet.config` — Job Processor registration/config
- `<job>.settings.xml` — job-specific runtime settings (preserve on upgrade)
- copy-local dependency DLLs (see each project's `bin\Release\` output)

Refer to [docs/job-collection.md](job-collection.md) for a description of every
job and its configuration files.
