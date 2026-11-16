using Autodesk.Connectivity.Explorer.Extensibility;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace adsk.ts.job.admin
{
    public partial class XtraFrmJobAdmin : DevExpress.XtraEditors.XtraForm
    {
        public XtraFrmJobAdmin()
        {
            InitializeComponent();
        }

        /// <summary>Adds the settings grid of each job to its tab page.</summary>
        public XtraFrmJobAdmin(IApplication application, bool canEdit) : this()
        {
            ApplyTheme(VDF.Forms.Library.CurrentTheme);
            VDF.Forms.Library.ThemeChanged += OnThemeChanged;
            FormClosed += (_, _) => VDF.Forms.Library.ThemeChanged -= OnThemeChanged;

            foreach (JobDefinition job in JobDefinition.All)
            {
                XtraTabPage? page = xtraTabControl1.TabPages.FirstOrDefault(p => p.Name == job.TabPageName);
                page?.Controls.Add(new JobSettingsPanel(job, application, canEdit));
            }
        }

        // ---------- theme ----------

        private void OnThemeChanged(object? sender, VDF.Forms.Library.UITheme theme)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() => ApplyTheme(theme)));
        }

        /// <summary>Picks the Vault light/dark skin explicitly, as the Vault custom theme skins require.</summary>
        private void ApplyTheme(VDF.Forms.Library.UITheme theme)
        {
            LookAndFeel.UseDefaultLookAndFeel = false;
            string themeName = theme.ToString();
            if (themeName == VDF.Forms.SkinUtils.Theme.Dark.ToString())
                LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.DarkThemeName);
            else if (themeName == VDF.Forms.SkinUtils.Theme.Light.ToString())
                LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.LightThemeName);
            else
                LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.DefaultThemeName);

            foreach (XtraTabPage page in xtraTabControl1.TabPages)
                foreach (JobSettingsPanel panel in page.Controls.OfType<JobSettingsPanel>())
                    panel.RefreshIcons();
        }
    }
}