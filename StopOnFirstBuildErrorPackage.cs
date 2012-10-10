#region License
/* 
StopOnFirstBuildError Visual Studio Extension
Copyright (C) 2011 Einar Egilsson

Contributors: Einar Egilsson, Steven Thuriot

http://einaregilsson.com/stop-build-on-first-error-in-visual-studio-2010/

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

		private const string ToggleShowErrorListCommandGuid = "817f6603-29ee-44bc-981e-3318336f0df6";
		private const uint ToggleShowErrorListCommandId = 0x101;

		private BuildEvents _buildEvents;
		private DTE2 _dte;
		private MenuCommand _menuItem;
		private MenuCommand _showErrorListMenuItem;
		private uint _selectionEventsCookie;
		private IVsMonitorSelection _selectionMonitor;
		private uint _solutionHasMultipleProjectsCookie;
		private bool _canExecute;

		public bool Enabled { get; set; }
		public bool Active { get; set; }
		public bool ShowErrorList { get; set; }

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
			_dte = (DTE2) GetGlobalService(typeof (DTE));
			Enabled = true;
			ShowErrorList = true;
			Active = true;
			_buildEvents = _dte.Events.BuildEvents;

			//Since Visual Studio 2012 has parallel builds, we only want to cancel the build process once.
			//This makes no difference for older versions of Visual Studio.
			_buildEvents.OnBuildBegin += delegate { _canExecute = true; };
			_buildEvents.OnBuildDone += delegate { _canExecute = false; };
			
			_buildEvents.OnBuildProjConfigDone += OnProjectBuildFinished;
			_selectionMonitor = (IVsMonitorSelection) GetGlobalService(typeof (SVsShellMonitorSelection));
			
			var solutionHasMultipleProjects = VSConstants.UICONTEXT.SolutionHasMultipleProjects_guid;
			
			_selectionMonitor.GetCmdUIContextCookie(ref solutionHasMultipleProjects, out _solutionHasMultipleProjectsCookie);
			_selectionMonitor.AdviseSelectionEvents(this, out _selectionEventsCookie);

			InitializeMenuItem();
		}

		private void InitializeMenuItem()
		{
			// Add our command handlers for menu (commands must exist in the .vsct file)
			var mcs = GetService(typeof (IMenuCommandService)) as OleMenuCommandService;
			if (mcs == null) return;

			// Create the command for the menu item.
			_menuItem = new MenuCommand(ToggleEnabled, new CommandID(new Guid(ToggleEnabledCommandGuid), (int) ToggleEnabledCommandId))
			            	{
								Checked = Enabled, 
								Visible = true
							};
			mcs.AddCommand(_menuItem);

			_showErrorListMenuItem = new MenuCommand(ToggleShowErrorList, new CommandID(new Guid(ToggleShowErrorListCommandGuid), (int)ToggleShowErrorListCommandId))
							{
								Checked = ShowErrorList,
								Visible = true
							};

			 mcs.AddCommand(_showErrorListMenuItem);
		}

		private void OnProjectBuildFinished(string project, string projectConfig, string platform, string solutionConfig, bool success)
		{
			if (!_canExecute || success || !Enabled || !Active) return;

            _canExecute = false;
			
			_dte.ExecuteCommand(CancelBuildCommand);

			var pane = _dte.ToolWindows.OutputWindow.OutputWindowPanes
									   .Cast<OutputWindowPane>()
									   .FirstOrDefault(x => x.Guid == BuildPaneGuid);

			if (pane != null)
			{
				var path = Path.GetFileNameWithoutExtension(project);
				var message = string.Format("StopOnFirstBuildError: Build cancelled because project \"{0}\" failed to build.{1}", path, Environment.NewLine);
				pane.OutputString(message);
			}

			if (ShowErrorList)
				_dte.ExecuteCommand(ViewErrorListCommand);
		}

		private void ToggleEnabled(object sender, EventArgs e)
		{
			Enabled = _menuItem.Checked = !_menuItem.Checked;
		}

		private void ToggleShowErrorList(object sender, EventArgs e)
		{
			ShowErrorList = _showErrorListMenuItem.Checked = !_showErrorListMenuItem.Checked;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_selectionMonitor.UnadviseSelectionEvents(_selectionEventsCookie);
			}

			base.Dispose(disposing);
		}
	}
}