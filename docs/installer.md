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

## Job Processor prerequisites

Some jobs require additional software on the machine running the Vault Job Processor:

| Job | Default engine | Requirement |
|---|---|---|
| `adsk.ts.pdf.create.office` | LibreOffice | Install [LibreOffice](https://www.libreoffice.org/) and ensure `soffice.exe` is available. Supports Microsoft Office Open XML (`.docx`, `.xlsx`, `.pptx`) and LibreOffice native formats (`.odt`, `.ods`, `.odp`, `.odg`, plus flat and template variants). No Microsoft Office license required. |
| `adsk.ts.pdf.create.office` | Microsoft Office | Install licensed Word, Excel, and PowerPoint desktop. Set `ConversionEngine=MicrosoftOffice` in `adsk.ts.pdf.create.office.settings.xml`. Review Microsoft's guidance on [server-side Office automation](https://support.microsoft.com/en-us/topic/considerations-for-server-side-automation-of-office-48bcfe93-8a89-47f1-0bce-017433ad79e2) before choosing this option. |
| Inventor-based jobs | — | Autodesk Inventor / VaultInventorServer |
| `adsk.ts.nwd.create.navisworks` | — | Autodesk Navisworks Manage |
| `adsk.ts.pdf.create.slddrw` | — | Autodesk SolidWorks |

When packaging the Office PDF job for `ConversionEngine=MicrosoftOffice`, copy the standard job output files only:

- `adsk.ts.pdf.create.office.dll`
- `adsk.ts.pdf.create.office.vcet.config`
- `adsk.ts.pdf.create.office.settings.xml`

No Office Primary Interop Assemblies are required at runtime; the job activates Word, Excel, and PowerPoint through COM ProgIDs.

After deployment, set `ConversionEngine` to `MicrosoftOffice` in the **Job Processor** copy of `adsk.ts.pdf.create.office.settings.xml` and restart the Job Processor service. Confirm the new build is loaded by checking the job log for `Assembly version: 31.0.84.5` or later.
