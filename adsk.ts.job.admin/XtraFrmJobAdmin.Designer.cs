namespace adsk.ts.job.admin
{
    partial class XtraFrmJobAdmin
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(XtraFrmJobAdmin));
            xtraTabControl1 = new DevExpress.XtraTab.XtraTabControl();
            acaddwg2DCreateInventor = new DevExpress.XtraTab.XtraTabPage();
            export3DCreateInventor = new DevExpress.XtraTab.XtraTabPage();
            imageCreateInventor = new DevExpress.XtraTab.XtraTabPage();
            rvtCreateInventor = new DevExpress.XtraTab.XtraTabPage();
            assignUpdateItem = new DevExpress.XtraTab.XtraTabPage();
            nwdCreateNavisworks = new DevExpress.XtraTab.XtraTabPage();
            pdfCreateOffice = new DevExpress.XtraTab.XtraTabPage();
            pdfCreateSldDrw = new DevExpress.XtraTab.XtraTabPage();
            ((System.ComponentModel.ISupportInitialize)xtraTabControl1).BeginInit();
            xtraTabControl1.SuspendLayout();
            SuspendLayout();
            // 
            // xtraTabControl1
            // 
            xtraTabControl1.Dock = DockStyle.Fill;
            xtraTabControl1.HeaderLocation = DevExpress.XtraTab.TabHeaderLocation.Left;
            xtraTabControl1.HeaderOrientation = DevExpress.XtraTab.TabOrientation.Horizontal;
            xtraTabControl1.Location = new Point(0, 0);
            xtraTabControl1.Name = "xtraTabControl1";
            xtraTabControl1.SelectedTabPage = acaddwg2DCreateInventor;
            xtraTabControl1.Size = new Size(747, 569);
            xtraTabControl1.TabIndex = 1;
            xtraTabControl1.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] { acaddwg2DCreateInventor, export3DCreateInventor, imageCreateInventor, rvtCreateInventor, assignUpdateItem, nwdCreateNavisworks, pdfCreateOffice, pdfCreateSldDrw });
            // 
            // acaddwg2DCreateInventor
            // 
            acaddwg2DCreateInventor.Name = "acaddwg2DCreateInventor";
            acaddwg2DCreateInventor.Size = new Size(596, 567);
            acaddwg2DCreateInventor.Text = "Inventor 2D DWG Export";
            // 
            // export3DCreateInventor
            // 
            export3DCreateInventor.Name = "export3DCreateInventor";
            export3DCreateInventor.Size = new Size(596, 567);
            export3DCreateInventor.Text = "Inventor 3D Exports";
            // 
            // imageCreateInventor
            // 
            imageCreateInventor.Name = "imageCreateInventor";
            imageCreateInventor.Size = new Size(596, 567);
            imageCreateInventor.Text = "Inventor Image Export";
            // 
            // rvtCreateInventor
            // 
            rvtCreateInventor.Name = "rvtCreateInventor";
            rvtCreateInventor.Size = new Size(596, 567);
            rvtCreateInventor.Text = "Inventor Revit Simplification";
            // 
            // assignUpdateItem
            // 
            assignUpdateItem.Name = "assignUpdateItem";
            assignUpdateItem.Size = new Size(596, 567);
            assignUpdateItem.Text = "Assign/Update Item";
            // 
            // nwdCreateNavisworks
            // 
            nwdCreateNavisworks.Name = "nwdCreateNavisworks";
            nwdCreateNavisworks.Size = new Size(596, 567);
            nwdCreateNavisworks.Text = "Navisworks Import";
            // 
            // pdfCreateOffice
            // 
            pdfCreateOffice.Name = "pdfCreateOffice";
            pdfCreateOffice.Size = new Size(596, 567);
            pdfCreateOffice.Text = "Office PDF";
            // 
            // pdfCreateSldDrw
            // 
            pdfCreateSldDrw.Name = "pdfCreateSldDrw";
            pdfCreateSldDrw.Size = new Size(596, 567);
            pdfCreateSldDrw.Text = "SolidWorks 2D PDF | DXF";
            // 
            // XtraFrmJobAdmin
            // 
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(747, 569);
            Controls.Add(xtraTabControl1);
            IconOptions.Icon = (Icon)resources.GetObject("XtraFrmJobAdmin.IconOptions.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Name = "XtraFrmJobAdmin";
            Text = "Job Collection | Configuration";
            ((System.ComponentModel.ISupportInitialize)xtraTabControl1).EndInit();
            xtraTabControl1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private DevExpress.XtraTab.XtraTabControl xtraTabControl1;
        private DevExpress.XtraTab.XtraTabPage acaddwg2DCreateInventor;
        private DevExpress.XtraTab.XtraTabPage export3DCreateInventor;
        private DevExpress.XtraTab.XtraTabPage imageCreateInventor;
        private DevExpress.XtraTab.XtraTabPage rvtCreateInventor;
        private DevExpress.XtraTab.XtraTabPage assignUpdateItem;
        private DevExpress.XtraTab.XtraTabPage nwdCreateNavisworks;
        private DevExpress.XtraTab.XtraTabPage pdfCreateOffice;
        private DevExpress.XtraTab.XtraTabPage pdfCreateSldDrw;
    }
}