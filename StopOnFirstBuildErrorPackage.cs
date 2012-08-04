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
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace EinarEgilsson.StopOnFirstBuildError
{
	[PackageRegistration(UseManagedResourcesOnly = true)]
	[InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuid)]
	[ProvideAutoLoad(UIContextGuids80.SolutionHasMultipleProjects)]
	public sealed class StopOnFirstBuildErrorPackage : Package, IVsSelectionEvents
	{
		private const string CancelBuildCommand = "Build.Cancel";
		private const string ViewErrorListCommand = "View.ErrorList";
		private const string PackageGuid = "5aa4f6e8-fb33-4bbe-9bea-05597fa6b071";
		private const string ToggleEnabledCommandGuid = "fddb8cf9-dce8-40c5-ba7f-8d93936e28f4";
		private const string BuildPaneGuid = "{1BD8A850-02D1-11D1-BEE7-00A0C913D1F8}";
		private const uint ToggleEnabledCommandId = 0x100;

		private BuildEvents _BuildEvents;
		private DTE2 _DTE;
		private MenuCommand _MenuItem;
		private uint _SelectionEventsCookie;
		private IVsMonitorSelection _SelectionMonitor;
		private uint _SolutionHasMultipleProjectsCookie;
		private bool _CanExecute;

		public bool Enabled { get; set; }
		public bool Active { get; set; }

		#region IVsSelectionEvents Members

		public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
		{
			if (_SolutionHasMultipleProjectsCookie == dwCmdUICookie)
			{
				_MenuItem.Visible = Active = fActive != 0;
			}
			return VSConstants.S_OK;
		}

		public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
		{
			return VSConstants.S_OK;
		}

		public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld,
		                              ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew,
		                              IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
		{
			return VSConstants.S_OK;
		}

		#endregion

		protected override void Initialize()
		{
			base.Initialize();
			_DTE = (DTE2) GetGlobalService(typeof (DTE));
			Enabled = true;
			Active = true;
			_BuildEvents = _DTE.Events.BuildEvents;

			//Since Visual Studio 2012 has parallel builds, we only want to cancel the build process once.
			//This makes no difference for older versions of Visual Studio.
			_BuildEvents.OnBuildBegin += delegate { _CanExecute = true; };
			_BuildEvents.OnBuildDone += delegate { _CanExecute = false; };
			
			_BuildEvents.OnBuildProjConfigDone += OnProjectBuildFinished;
			_SelectionMonitor = (IVsMonitorSelection) GetGlobalService(typeof (SVsShellMonitorSelection));
			
			var solutionHasMultipleProjects = VSConstants.UICONTEXT.SolutionHasMultipleProjects_guid;
			
			_SelectionMonitor.GetCmdUIContextCookie(ref solutionHasMultipleProjects, out _SolutionHasMultipleProjectsCookie);
			_SelectionMonitor.AdviseSelectionEvents(this, out _SelectionEventsCookie);

			InitializeMenuItem();
		}

		private void InitializeMenuItem()
		{
			// Add our command handlers for menu (commands must exist in the .vsct file)
			var mcs = GetService(typeof (IMenuCommandService)) as OleMenuCommandService;
			if (mcs == null) return;

			// Create the command for the menu item.
			_MenuItem = new MenuCommand(ToggleEnabled, new CommandID(new Guid(ToggleEnabledCommandGuid), (int) ToggleEnabledCommandId))
			            	{
								Checked = Enabled, 
								Visible = true
							};
			mcs.AddCommand(_MenuItem);
		}

		private void OnProjectBuildFinished(string project, string projectConfig, string platform, string solutionConfig, bool success)
		{
			if (!_CanExecute || success || !Enabled || !Active) return;

            _CanExecute = false;
			
			_DTE.ExecuteCommand(CancelBuildCommand);

			var pane = _DTE.ToolWindows.OutputWindow.OutputWindowPanes
									   .Cast<OutputWindowPane>()
									   .FirstOrDefault(x => x.Guid == BuildPaneGuid);

			if (pane != null)
			{
				var path = Path.GetFileNameWithoutExtension(project);
				var message = string.Format("StopOnFirstBuildError: Build cancelled because project \"{0}\" failed to build.{1}", path, Environment.NewLine);
				pane.OutputString(message);
			}

			_DTE.ExecuteCommand(ViewErrorListCommand);
		}

		private void ToggleEnabled(object sender, EventArgs e)
		{
			Enabled = _MenuItem.Checked = !_MenuItem.Checked;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_SelectionMonitor.UnadviseSelectionEvents(_SelectionEventsCookie);
			}

			base.Dispose(disposing);
		}
	}
}