Place 16x16 (or larger) file-type PNGs here before running tools/Sync-JobSubmissionIcons.ps1.

Expected names (extension association icons are auto-captured when missing):
  png.png   - Inventor image export (ImgFileType sample: PNG)
  pdf.png   - PDF outputs (Office, SolidWorks drawing)
  nwd.png   - Navisworks NWD (DWF also produced when ExportFormats=NWD+DWF on Job Processor)
  rvt.png   - Revit RVT simplification export
  dwg.png   - Inventor 2D DWG export
  dwg.png   - Also used for the Create Inventor 3D DWG, JT, ... menu (3D DWG is a primary output; Job Processor ExportFormats controls all outputs)

Existing files are never overwritten by the sync script.
