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
You can author your own WiX (or other) packaging project against these outputs.

### Job Processor (server)

Each job's Vault Job Processor registration expects the following per job:

- `<job>.dll` — main assembly
- `<job>.vcet.config` — Job Processor registration/config
- `<job>.settings.xml` — default job settings; copy to the target folder if newer (do not preserve on upgrade, see [Job settings and upgrades](#job-settings-and-upgrades))
- copy-local dependency DLLs (see each project's `bin\Release\` output)

Refer to [docs/job-collection.md](job-collection.md) for a description of every
job and its configuration files.

#### Job settings and upgrades

The `<job>.settings.xml` files only provide the **defaults and the fallback**. Administrators change settings
in the **Job Collection configuration** dialog (Vault Explorer, **Tools** menu, `adsk.ts.job.admin`), which saves
them as Vault options (`adsk.ts.job.settings.<job assembly name>`). A job applies the values saved in Vault on top
of its local XML, and the local file is never modified by the dialog.

Because administrator edits live in Vault, the installer must not preserve the XML files on upgrade. Package them
so that an existing file is replaced **only if the packaged file is newer** ("copy if newer"), the same behavior as
`CopyToOutputDirectory=PreserveNewest` in the job projects. Settings saved in Vault survive every upgrade.

### Vault Client (workstations)

To allow manual job queueing from Vault Explorer, deploy the client extension on **user workstations** (in addition to Job Processor binaries on the server):

| File | Purpose |
|---|---|
| `adsk.ts.explorer.job.submit.dll` | Explorer `IExplorerExtension` |
| `adsk.ts.explorer.job.submit.vcet.config` | Client extension registration |

Default folder: `%ProgramData%\Autodesk\Vault 2027\Extensions\adsk.ts.job.collection\`. See [Manual submission (Vault Explorer)](job-collection.md#manual-submission-vault-explorer) in `job-collection.md`.

## Job Processor prerequisites

Some jobs require additional software on the machine running the Vault Job Processor:

| Job | Default engine | Requirement |
|---|---|---|
| `adsk.ts.pdf.create.office` | LibreOffice | Install [LibreOffice](https://www.libreoffice.org/) and ensure `soffice.exe` is available. Supports Microsoft Office Open XML (`.docx`, `.xlsx`, `.pptx`) and LibreOffice native formats (`.odt`, `.ods`, `.odp`, `.odg`, plus flat and template variants). No Microsoft Office license required. |
| `adsk.ts.pdf.create.office` | Microsoft Office | Install licensed Word, Excel, and PowerPoint desktop. Select **MicrosoftOffice** as conversion engine in the Job Collection configuration dialog (or set `ConversionEngine=MicrosoftOffice` in `adsk.ts.pdf.create.office.settings.xml` as default). Review Microsoft's guidance on [server-side Office automation](https://support.microsoft.com/en-us/topic/considerations-for-server-side-automation-of-office-48bcfe93-8a89-47f1-0bce-017433ad79e2) before choosing this option. |
| Inventor-based jobs | — | Autodesk Inventor / VaultInventorServer |
| `adsk.ts.nwd.create.navisworks` | — | Autodesk Navisworks Manage |
| `adsk.ts.pdf.create.slddrw` | — | Autodesk SolidWorks |

When packaging the Office PDF job for `ConversionEngine=MicrosoftOffice`, copy the standard job output files only:

- `adsk.ts.pdf.create.office.dll`
- `adsk.ts.pdf.create.office.vcet.config`
- `adsk.ts.pdf.create.office.settings.xml`

No Office Primary Interop Assemblies are required at runtime; the job activates Word, Excel, and PowerPoint through COM ProgIDs.

After deployment, select **MicrosoftOffice** as conversion engine on the Office PDF tab of the Job Collection configuration dialog; it is saved as Vault option and applies to all Job Processors. `MicrosoftOfficeConversionTimeoutSeconds` defaults to `180` if missing. Restart the Job Processor service. Confirm the new build is loaded by checking the job log for `Assembly version: 31.0.85.1` or later.
