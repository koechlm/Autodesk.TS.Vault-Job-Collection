# Extended Vault API Samples - WiP
## Detailed Documentation: [docs\job-collection.md](docs/job-collection.md).
## Installer

An MSI installer for this job collection is maintained in a private repository and is not
distributed as public source. See [docs\installer.md](docs/installer.md) for details, or
contact the maintainer for access.

## Export Jobs running VaultInventorServer, Inventor, Navisworks, Solidworks, and Office PDF
<img width="2560" height="1439" alt="image" src="https://github.com/user-attachments/assets/80e38b20-26b4-45a1-82a4-cb2df4ba3aaa" />
<img width="1033" height="289" alt="image" src="https://github.com/user-attachments/assets/82b1c246-7efc-4130-86ea-8b31b06612a0" />

## Job projects

| Project | Job type | Host application |
|---|---|---|
| `adsk.ts.acad.dwg2d.create.inventor` | 2D DWG export | Inventor / VaultInventorServer |
| `adsk.ts.export3d.create.inventor` | 3D neutral export | Inventor / VaultInventorServer |
| `adsk.ts.image.create.inventor` | Image export | Inventor / VaultInventorServer |
| `adsk.ts.rvt.create.inventor` | Revit simplification export | Inventor / VaultInventorServer |
| `adsk.ts.nwd.create.navisworks` | NWD export | Navisworks Manage |
| `adsk.ts.pdf.create.slddrw` | PDF/DXF export | SolidWorks |
| `adsk.ts.pdf.create.office` | Office PDF export | LibreOffice *(default)* or Microsoft Office |
| `adsk.ts.AssignUpdateFmItem` | Item assignment / Fusion Manage sync | Vault API |

See [docs/job-collection.md](docs/job-collection.md) for settings, lifecycle job rule examples, and deployment prerequisites.
