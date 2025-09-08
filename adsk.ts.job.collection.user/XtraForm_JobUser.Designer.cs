namespace adsk.ts.job.collection.user
{
    partial class XtraForm_JobUser
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(XtraForm_JobUser));
            groupControl1 = new DevExpress.XtraEditors.GroupControl();
            cmbPriorityGlobal = new DevExpress.XtraEditors.ComboBoxEdit();
            labelControl1 = new DevExpress.XtraEditors.LabelControl();
            lblGlobalJob = new DevExpress.XtraEditors.LabelControl();
            cmbJobGlobal = new DevExpress.XtraEditors.ComboBoxEdit();
            groupControl2 = new DevExpress.XtraEditors.GroupControl();
            btnRemoveFile = new DevExpress.XtraEditors.SimpleButton();
            grdFiles = new DevExpress.XtraGrid.GridControl();
            gridView1 = new DevExpress.XtraGrid.Views.Grid.GridView();
            Order = new DevExpress.XtraGrid.Columns.GridColumn();
            FileId = new DevExpress.XtraGrid.Columns.GridColumn();
            FileName = new DevExpress.XtraGrid.Columns.GridColumn();
            Extension = new DevExpress.XtraGrid.Columns.GridColumn();
            JobName = new DevExpress.XtraGrid.Columns.GridColumn();
            JobPriority = new DevExpress.XtraGrid.Columns.GridColumn();
            btnMoveDown = new DevExpress.XtraEditors.SimpleButton();
            btnAddFile = new DevExpress.XtraEditors.SimpleButton();
            btnMoveUp = new DevExpress.XtraEditors.SimpleButton();
            btnCancel = new DevExpress.XtraEditors.SimpleButton();
            btnSubmit = new DevExpress.XtraEditors.SimpleButton();
            persistentRepository1 = new DevExpress.XtraEditors.Repository.PersistentRepository(components);
            ((System.ComponentModel.ISupportInitialize)groupControl1).BeginInit();
            groupControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)cmbPriorityGlobal.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)cmbJobGlobal.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)groupControl2).BeginInit();
            groupControl2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)grdFiles).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridView1).BeginInit();
            SuspendLayout();
            // 
            // groupControl1
            // 
            groupControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupControl1.Controls.Add(cmbPriorityGlobal);
            groupControl1.Controls.Add(labelControl1);
            groupControl1.Controls.Add(lblGlobalJob);
            groupControl1.Controls.Add(cmbJobGlobal);
            groupControl1.Location = new System.Drawing.Point(12, 12);
            groupControl1.Name = "groupControl1";
            groupControl1.Size = new System.Drawing.Size(691, 88);
            groupControl1.TabIndex = 0;
            groupControl1.Text = "Default Job Selection | Priority";
            // 
            // cmbPriorityGlobal
            // 
            cmbPriorityGlobal.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            cmbPriorityGlobal.EditValue = 100;
            cmbPriorityGlobal.Location = new System.Drawing.Point(486, 52);
            cmbPriorityGlobal.Name = "cmbPriorityGlobal";
            cmbPriorityGlobal.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo) });
            cmbPriorityGlobal.Properties.Items.AddRange(new object[] { "10", "20", "30", "40", "50", "60", "70", "80", "90", "100", "110", "120", "130", "140", "150" });
            cmbPriorityGlobal.Properties.MaxLength = 4;
            cmbPriorityGlobal.Properties.Name = "cmbPriorityGlobal";
            cmbPriorityGlobal.Size = new System.Drawing.Size(62, 20);
            cmbPriorityGlobal.TabIndex = 3;
            cmbPriorityGlobal.SelectedValueChanged += cmbPriorityGlobal_SelectedValueChanged;
            // 
            // labelControl1
            // 
            labelControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            labelControl1.Location = new System.Drawing.Point(322, 55);
            labelControl1.Name = "labelControl1";
            labelControl1.Size = new System.Drawing.Size(158, 13);
            labelControl1.TabIndex = 2;
            labelControl1.Text = "Set Job Priority for Selected Files";
            // 
            // lblGlobalJob
            // 
            lblGlobalJob.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            lblGlobalJob.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Horizontal;
            lblGlobalJob.Location = new System.Drawing.Point(333, 29);
            lblGlobalJob.Name = "lblGlobalJob";
            lblGlobalJob.Size = new System.Drawing.Size(147, 13);
            lblGlobalJob.TabIndex = 1;
            lblGlobalJob.Text = "Set Job Type for selected Files";
            // 
            // cmbJobGlobal
            // 
            cmbJobGlobal.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            cmbJobGlobal.Location = new System.Drawing.Point(486, 26);
            cmbJobGlobal.Name = "cmbJobGlobal";
            cmbJobGlobal.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo) });
            cmbJobGlobal.Size = new System.Drawing.Size(171, 20);
            cmbJobGlobal.TabIndex = 0;
            cmbJobGlobal.ToolTip = "Select a Job applying to all files listed below.";
            cmbJobGlobal.ToolTipTitle = "Global Job Selection";
            cmbJobGlobal.SelectedValueChanged += cmbJobGlobal_SelectedValueChanged;
            // 
            // groupControl2
            // 
            groupControl2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupControl2.Controls.Add(btnRemoveFile);
            groupControl2.Controls.Add(grdFiles);
            groupControl2.Controls.Add(btnMoveDown);
            groupControl2.Controls.Add(btnAddFile);
            groupControl2.Controls.Add(btnMoveUp);
            groupControl2.Location = new System.Drawing.Point(12, 106);
            groupControl2.Name = "groupControl2";
            groupControl2.Size = new System.Drawing.Size(691, 298);
            groupControl2.TabIndex = 1;
            groupControl2.Text = "Files to Process | Individual Job Selection and Priority";
            // 
            // btnRemoveFile
            // 
            btnRemoveFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnRemoveFile.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("btnRemoveFile.ImageOptions.Image");
            btnRemoveFile.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            btnRemoveFile.Location = new System.Drawing.Point(663, 146);
            btnRemoveFile.Name = "btnRemoveFile";
            btnRemoveFile.Size = new System.Drawing.Size(23, 23);
            btnRemoveFile.TabIndex = 11;
            btnRemoveFile.ToolTip = "Remove selected files from job list.";
            btnRemoveFile.ToolTipTitle = "Remove File(s)";
            btnRemoveFile.Click += btnRemoveFile_Click;
            // 
            // grdFiles
            // 
            grdFiles.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            grdFiles.Location = new System.Drawing.Point(5, 26);
            grdFiles.MainView = gridView1;
            grdFiles.Name = "grdFiles";
            grdFiles.Size = new System.Drawing.Size(652, 267);
            grdFiles.TabIndex = 0;
            grdFiles.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { gridView1 });
            // 
            // gridView1
            // 
            gridView1.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { Order, FileId, FileName, Extension, JobName, JobPriority });
            gridView1.GridControl = grdFiles;
            gridView1.Name = "gridView1";
            gridView1.OptionsBehavior.AutoExpandAllGroups = true;
            gridView1.OptionsLayout.Columns.RemoveOldColumns = false;
            gridView1.OptionsMenu.EnableColumnMenu = false;
            gridView1.OptionsSelection.MultiSelect = true;
            gridView1.SortInfo.AddRange(new DevExpress.XtraGrid.Columns.GridColumnSortInfo[] { new DevExpress.XtraGrid.Columns.GridColumnSortInfo(Order, DevExpress.Data.ColumnSortOrder.Ascending) });
            // 
            // Order
            // 
            Order.AppearanceCell.Options.UseTextOptions = true;
            Order.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            Order.Caption = "Job Order";
            Order.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            Order.FieldName = "order";
            Order.Fixed = DevExpress.XtraGrid.Columns.FixedStyle.Left;
            Order.GroupFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            Order.Name = "Order";
            Order.OptionsColumn.AllowEdit = false;
            Order.OptionsColumn.AllowGroup = DevExpress.Utils.DefaultBoolean.False;
            Order.OptionsColumn.AllowMove = false;
            Order.OptionsColumn.AllowShowHide = false;
            Order.OptionsColumn.ReadOnly = true;
            Order.ToolTip = "Transfer sequence to job queue. Does not change job execution priority.";
            Order.UnboundDataType = typeof(long);
            Order.Visible = true;
            Order.VisibleIndex = 0;
            Order.Width = 78;
            // 
            // FileId
            // 
            FileId.Caption = "Master Id";
            FileId.FieldName = "fileid";
            FileId.Name = "FileId";
            FileId.UnboundDataType = typeof(long);
            // 
            // FileName
            // 
            FileName.Caption = "File Name";
            FileName.FieldName = "filename";
            FileName.Name = "FileName";
            FileName.OptionsColumn.AllowEdit = false;
            FileName.OptionsColumn.AllowGroup = DevExpress.Utils.DefaultBoolean.False;
            FileName.OptionsColumn.AllowMove = false;
            FileName.OptionsColumn.AllowShowHide = false;
            FileName.OptionsColumn.ReadOnly = true;
            FileName.Visible = true;
            FileName.VisibleIndex = 1;
            FileName.Width = 159;
            // 
            // Extension
            // 
            Extension.Caption = "File Type";
            Extension.FieldName = "extension";
            Extension.Name = "Extension";
            Extension.OptionsColumn.AllowEdit = false;
            Extension.OptionsColumn.AllowGroup = DevExpress.Utils.DefaultBoolean.True;
            Extension.OptionsColumn.AllowShowHide = false;
            Extension.Visible = true;
            Extension.VisibleIndex = 2;
            Extension.Width = 62;
            // 
            // JobName
            // 
            JobName.Caption = "Job Name";
            JobName.FieldName = "jobname";
            JobName.Name = "JobName";
            JobName.OptionsColumn.AllowGroup = DevExpress.Utils.DefaultBoolean.True;
            JobName.OptionsColumn.AllowShowHide = false;
            JobName.Visible = true;
            JobName.VisibleIndex = 3;
            JobName.Width = 220;
            // 
            // JobPriority
            // 
            JobPriority.AppearanceCell.Options.UseTextOptions = true;
            JobPriority.AppearanceCell.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            JobPriority.Caption = "Job Priority";
            JobPriority.DisplayFormat.FormatString = "###";
            JobPriority.DisplayFormat.FormatType = DevExpress.Utils.FormatType.Numeric;
            JobPriority.FieldName = "priority";
            JobPriority.Name = "JobPriority";
            JobPriority.OptionsColumn.AllowGroup = DevExpress.Utils.DefaultBoolean.True;
            JobPriority.OptionsColumn.AllowShowHide = false;
            JobPriority.ToolTip = "Job execution priority, 1 = highest priority, 100 = default priority. Overrules submission order.";
            JobPriority.UnboundDataType = typeof(int);
            JobPriority.Visible = true;
            JobPriority.VisibleIndex = 4;
            JobPriority.Width = 106;
            // 
            // btnMoveDown
            // 
            btnMoveDown.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnMoveDown.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("btnMoveDown.ImageOptions.Image");
            btnMoveDown.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            btnMoveDown.Location = new System.Drawing.Point(663, 117);
            btnMoveDown.Name = "btnMoveDown";
            btnMoveDown.Size = new System.Drawing.Size(23, 23);
            btnMoveDown.TabIndex = 10;
            btnMoveDown.ToolTip = "Submit later.";
            btnMoveDown.ToolTipTitle = "Order of Transfer";
            btnMoveDown.Click += btnMoveDown_Click;
            // 
            // btnAddFile
            // 
            btnAddFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnAddFile.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("btnAddFile.ImageOptions.Image");
            btnAddFile.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            btnAddFile.Location = new System.Drawing.Point(663, 59);
            btnAddFile.Name = "btnAddFile";
            btnAddFile.Size = new System.Drawing.Size(23, 23);
            btnAddFile.TabIndex = 8;
            btnAddFile.ToolTip = "Add file(s) to job list.";
            btnAddFile.ToolTipTitle = "Add Files";
            // 
            // btnMoveUp
            // 
            btnMoveUp.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnMoveUp.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("btnMoveUp.ImageOptions.Image");
            btnMoveUp.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            btnMoveUp.Location = new System.Drawing.Point(663, 88);
            btnMoveUp.Name = "btnMoveUp";
            btnMoveUp.Size = new System.Drawing.Size(23, 23);
            btnMoveUp.TabIndex = 9;
            btnMoveUp.ToolTip = "Submit earlier.";
            btnMoveUp.ToolTipTitle = "Order of Transfer";
            btnMoveUp.Click += btnMoveUp_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnCancel.Location = new System.Drawing.Point(628, 410);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 23);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;
            // 
            // btnSubmit
            // 
            btnSubmit.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnSubmit.Location = new System.Drawing.Point(525, 410);
            btnSubmit.Name = "btnSubmit";
            btnSubmit.Size = new System.Drawing.Size(97, 23);
            btnSubmit.TabIndex = 3;
            btnSubmit.Text = "Submit to Queue";
            btnSubmit.Click += btnSubmit_Click;
            // 
            // XtraForm_JobUser
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(715, 445);
            Controls.Add(btnSubmit);
            Controls.Add(btnCancel);
            Controls.Add(groupControl2);
            Controls.Add(groupControl1);
            IconOptions.Icon = (System.Drawing.Icon)resources.GetObject("XtraForm_JobUser.IconOptions.Icon");
            MinimumSize = new System.Drawing.Size(600, 400);
            Name = "XtraForm_JobUser";
            Text = "TS Job Collection | Submit to Queue";
            Load += XtraForm_JobUser_Load;
            ((System.ComponentModel.ISupportInitialize)groupControl1).EndInit();
            groupControl1.ResumeLayout(false);
            groupControl1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)cmbPriorityGlobal.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)cmbJobGlobal.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)groupControl2).EndInit();
            groupControl2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)grdFiles).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DevExpress.XtraEditors.GroupControl groupControl1;
        private DevExpress.XtraEditors.ComboBoxEdit cmbJobGlobal;
        private DevExpress.XtraEditors.GroupControl groupControl2;
        private DevExpress.XtraEditors.SimpleButton btnCancel;
        private DevExpress.XtraEditors.SimpleButton btnSubmit;
        private DevExpress.XtraEditors.SimpleButton btnAddFile;
        private DevExpress.XtraEditors.SimpleButton btnMoveUp;
        private DevExpress.XtraEditors.SimpleButton btnMoveDown;
        private DevExpress.XtraEditors.SimpleButton btnRemoveFile;
        private DevExpress.XtraGrid.GridControl grdFiles;
        private DevExpress.XtraGrid.Views.Grid.GridView gridView1;
        private DevExpress.XtraGrid.Columns.GridColumn Order;
        private DevExpress.XtraGrid.Columns.GridColumn JobName;
        private DevExpress.XtraGrid.Columns.GridColumn JobPriority;
        private DevExpress.XtraEditors.Repository.PersistentRepository persistentRepository1;
        private DevExpress.XtraGrid.Columns.GridColumn FileId;
        private DevExpress.XtraEditors.LabelControl lblGlobalJob;
        private DevExpress.XtraEditors.ComboBoxEdit cmbPriorityGlobal;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraGrid.Columns.GridColumn FileName;
        private DevExpress.XtraGrid.Columns.GridColumn Extension;
    }
}