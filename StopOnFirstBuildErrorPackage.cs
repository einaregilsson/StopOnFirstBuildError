#region License
/* 
StopOnFirstBuildError Visual Studio Extension
Copyright (C) 2011 Einar Egilsson
http://tech.einaregilsson.com/2011/01/06/stop-build-on-first-error-in-visual-studio-2010/

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

$Id$ 
*/
#endregion
using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE80;
using EnvDTE;
using System.Windows.Forms;

namespace EinarEgilsson.StopOnFirstBuildError
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(StopOnFirstBuildErrorPackage.PackageGuid)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasMultipleProjects)]
    public sealed class StopOnFirstBuildErrorPackage : Package, IVsSelectionEvents
    {
        const string CancelBuildCommand = "Build.Cancel";
        const string ViewErrorListCommand = "View.ErrorList";
        const string PackageGuid = "5aa4f6e8-fb33-4bbe-9bea-05597fa6b071";
        const string ToggleEnabledCommandGuid = "fddb8cf9-dce8-40c5-ba7f-8d93936e28f4";
        const string BuildPaneGuid = "{1BD8A850-02D1-11D1-BEE7-00A0C913D1F8}";
        const uint ToggleEnabledCommandId = 0x100;

        DTE2 _dte;
        BuildEvents _buildEvents;
        IVsMonitorSelection _selectionMonitor;
        uint _selectionEventsCookie;
        uint _solutionHasMultipleProjectsCookie;
        Guid _solutionWithMultipleProjectsGuid;
        MenuCommand _menuItem;

        public bool Enabled { get; set; }
        public bool Active { get; set; }

        protected override void Initialize()
        {
            base.Initialize();
            _dte = (DTE2)GetGlobalService(typeof(DTE));
            Enabled = true;
            Active = true;
            _buildEvents = _dte.Events.BuildEvents;
            _buildEvents.OnBuildProjConfigDone += OnProjectBuildFinished;
            _selectionMonitor = (IVsMonitorSelection)GetGlobalService(typeof(SVsShellMonitorSelection));
            Guid solutionHasMultipleProjects = VSConstants.UICONTEXT.SolutionHasMultipleProjects_guid;
            _selectionMonitor.GetCmdUIContextCookie(ref solutionHasMultipleProjects, out _solutionHasMultipleProjectsCookie);
            _selectionMonitor.AdviseSelectionEvents(this, out _selectionEventsCookie);
            
            InitializeMenuItem();
        }

        private void InitializeMenuItem()
        {
            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs == null) return;
            
            // Create the command for the menu item.
            _menuItem = new MenuCommand(ToggleEnabled, new CommandID(new Guid(ToggleEnabledCommandGuid), (int)ToggleEnabledCommandId));
            _menuItem.Checked = Enabled;
            _menuItem.Visible = true;
            mcs.AddCommand(_menuItem);
        }

        void OnProjectBuildFinished(string project, string projectConfig, string platform, string solutionConfig, bool success)
        {
            if (success || !Enabled || !Active) return;
            _dte.ExecuteCommand(CancelBuildCommand);

            foreach (OutputWindowPane pane in _dte.ToolWindows.OutputWindow.OutputWindowPanes)
            {
                if (pane.Guid == BuildPaneGuid) {
                    pane.OutputString("StopOnFirstBuildError: Build cancelled because project " + Path.GetFileNameWithoutExtension(project) + " failed to build.\r\n");
                    break;
                }
            }
            _dte.ExecuteCommand(ViewErrorListCommand);
        }

        private void ToggleEnabled(object sender, EventArgs e)
        {
            Enabled = _menuItem.Checked = !_menuItem.Checked;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _selectionMonitor.UnadviseSelectionEvents(_selectionEventsCookie);
            }
            base.Dispose(disposing);
            
        }

        #region IVsSelectionEvents Members

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            if (_solutionHasMultipleProjectsCookie == dwCmdUICookie)
            {
                _menuItem.Visible = Active = fActive != 0;
            }
            return VSConstants.S_OK;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            return VSConstants.S_OK;
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        #endregion
    }
}
