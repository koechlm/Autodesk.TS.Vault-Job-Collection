using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.Utils.MVVM;
using DevExpress.CodeParser;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using static DevExpress.XtraPrinting.Native.ExportOptionsPropertiesNames;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace adsk.ts.job.collection.user
{
    public partial class XtraForm_JobUser : DevExpress.XtraEditors.XtraForm
    {
        // fill the job list with Job names from the resources
        internal static string[] joblist = new string[] {
            Properties.Resources.None,
            Properties.Resources.adsk_ts_acad_dwg2d_create_inventor,
            Properties.Resources.adsk_ts_export3d_create_inventor,
            Properties.Resources.adsk_ts_image_create_inventor,
            Properties.Resources.adsk_ts_rvt_create_inventor,
            Properties.Resources.adsk_ts_nwd_create_navisworks,
            Properties.Resources.adsk_ts_pdf_create_slddrw
        };

        // Vault themes support
        private string mCurrentTheme;


        public XtraForm_JobUser()
        {
            InitializeComponent();

            // set the current theme
            mCurrentTheme = VDF.Forms.SkinUtils.WinFormsTheme.Instance.CurrentTheme.ToString();

            if (mCurrentTheme == VDF.Forms.SkinUtils.Theme.Light.ToString())
            {
                this.LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.LightThemeName);
            }
            if (mCurrentTheme == VDF.Forms.SkinUtils.Theme.Dark.ToString())
            {
                this.LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.DarkThemeName);
            }
            if (mCurrentTheme == VDF.Forms.SkinUtils.Theme.Default.ToString())
            {
                this.LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.DefaultThemeName);
            }
            // load dark or light (default) icons
            if (mCurrentTheme == VDF.Forms.SkinUtils.Theme.Dark.ToString())
            {
                btnAddFile.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.Add_Generic_16_dark);
                btnRemoveFile.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.Remove_Generic_16_dark);
                btnMoveUp.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.ArrowUp_16_dark);
                btnMoveDown.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.ArrowDown_16_dark);
            }
            else
            {
                btnAddFile.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.Add_Generic_16_light);
                btnRemoveFile.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.Remove_Generic_16_light);
                btnMoveUp.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.ArrowUp_16_light);
                btnMoveDown.ImageOptions.Image = ExplorerExtension.ConvertByteArrayToImage(Properties.Resources.ArrowDown_16_light);
            }

            // fill the combobox with the job names
            cmbJobGlobal.Properties.Items.AddRange(joblist);
            // restrict the combobox to the list only
            cmbJobGlobal.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;

            // restrict the combobox to the list only
            cmbPriorityGlobal.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;

            // disable until the gridview has rows selected
            cmbJobGlobal.Enabled = false;
            cmbPriorityGlobal.Enabled = false;
            gridView1.SelectionChanged += gridView1_SelectionChanged;

            // fill the combobox with the job names to be added to the grid column jobname
            RepositoryItemComboBox itemComboBox = new RepositoryItemComboBox();
            itemComboBox.Items.AddRange(joblist);
            grdFiles.RepositoryItems.Add(itemComboBox);
            JobName.ColumnEdit = itemComboBox;

            // Initialize DataTable for grdFiles
            var table = new DataTable();
            table.Columns.Add("order", typeof(long));
            table.Columns.Add("fileid", typeof(long));
            table.Columns.Add("filename", typeof(string));
            table.Columns.Add("extension", typeof(string));
            table.Columns.Add("jobname", typeof(string));
            table.Columns.Add("priority", typeof(int));
            grdFiles.DataSource = table;
        }

        internal void AddFileToList(long id, string filename)
        {
            // add a new row the grid with the file id and name
            DataRow row = (grdFiles.DataSource as DataTable).NewRow();
            row["order"] = (grdFiles.DataSource as DataTable).Rows.Count;
            row["filename"] = filename;
            row["extension"] = System.IO.Path.GetExtension(filename).TrimStart('.').ToUpper();
            row["jobname"] = Properties.Resources.None;
            row["priority"] = 100;

            // refresh the grid
            (grdFiles.DataSource as DataTable).Rows.Add(row);
        }

        private void grdFiles_Click(object sender, EventArgs e)
        {

        }

        private void XtraForm_JobUser_Load(object sender, EventArgs e)
        {

        }

        private void cmbJobGlobal_SelectedValueChanged(object sender, EventArgs e)
        {
            // copy the value of the combobox to the selected rows in the grid column jobname
            if (gridView1.SelectedRowsCount != 0)
            {
                foreach (int i in gridView1.GetSelectedRows())
                {
                    gridView1.SetRowCellValue(i, "jobname", cmbJobGlobal.SelectedItem.ToString());
                }
            }     
            
            gridView1.RefreshData();
        }

        private void cmbPriorityGlobal_SelectedValueChanged(object sender, EventArgs e)
        {
            // copa the value of the combobox to the selected rows in the grid column priority
            if (gridView1.SelectedRowsCount != 0)
            {
                foreach (int i in gridView1.GetSelectedRows())
                {
                    gridView1.SetRowCellValue(i, "priority", Convert.ToInt32(cmbPriorityGlobal.SelectedItem.ToString()));
                }
            }

            gridView1.RefreshData();
        }

        // Event handler
        private void gridView1_SelectionChanged(object sender, DevExpress.Data.SelectionChangedEventArgs e)
        {
            // Enable/disable controls based on selection count
            bool hasSelection = gridView1.SelectedRowsCount > 0;
            cmbJobGlobal.Enabled = hasSelection;
            cmbPriorityGlobal.Enabled = hasSelection;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            // close the form
            this.Close();
        }

        private void btnSubmit_Click(object sender, EventArgs e)
        {
            // validate that all rows have a jobname assigned and that the priority is between 1 and 1000


            // return the DialogResult OK and close the form
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            // move the selected rows up
            int[] selectedRows = gridView1.GetSelectedRows();
            foreach (int rowHandle in selectedRows)
            {
                if (rowHandle > 0)
                {
                    // Swap the values of the current row and the row above it
                    var currentRow = gridView1.GetDataRow(rowHandle);
                    var previousRow = gridView1.GetDataRow(rowHandle - 1);

                    if (currentRow != null && previousRow != null)
                    {
                        var tempOrder = currentRow["order"];
                        currentRow["order"] = previousRow["order"];
                        previousRow["order"] = tempOrder;

                        var tempRow = currentRow.ItemArray;
                        currentRow.ItemArray = previousRow.ItemArray;
                        previousRow.ItemArray = tempRow;
                    }

                    // update the selected rows to the new position
                    gridView1.SelectRow(rowHandle - 1);
                    gridView1.UnselectRow(rowHandle);

                    // Refresh the grid to reflect the changes
                    gridView1.RefreshData();
                }
            }
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            // move the selected rows down
            int[] selectedRows = gridView1.GetSelectedRows();
            for (int i = selectedRows.Length - 1; i >= 0; i--)
            {
                int rowHandle = selectedRows[i];
                if (rowHandle < gridView1.RowCount - 1)
                {
                    // Swap the values of the current row and the row below it
                    var currentRow = gridView1.GetDataRow(rowHandle);
                    var nextRow = gridView1.GetDataRow(rowHandle + 1);
                    if (currentRow != null && nextRow != null)
                    {
                        var tempOrder = currentRow["order"];
                        currentRow["order"] = nextRow["order"];
                        nextRow["order"] = tempOrder;
                        var tempRow = currentRow.ItemArray;
                        currentRow.ItemArray = nextRow.ItemArray;
                        nextRow.ItemArray = tempRow;
                    }

                    // update the selected rows to the new position
                    gridView1.SelectRow(rowHandle + 1);
                    gridView1.UnselectRow(rowHandle);

                    // Refresh the grid to reflect the changes
                    gridView1.RefreshData();
                }
            }
        }

        private void btnRemoveFile_Click(object sender, EventArgs e)
        {
            // remove the selected rows from the grid
            int[] selectedRows = gridView1.GetSelectedRows();
            for (int i = selectedRows.Length - 1; i >= 0; i--)
            {
                int rowHandle = selectedRows[i];
                gridView1.DeleteRow(rowHandle);
            }
            gridView1.RefreshData();
        }
    }
}