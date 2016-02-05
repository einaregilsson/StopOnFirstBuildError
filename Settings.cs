using System;
using System.ComponentModel;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace EinarEgilsson.StopOnFirstBuildError
{
    public class Settings : DialogPage
    {
        private bool _enabled = true;
        private bool _showErrorList;

        public event EventHandler EnabledChanged;
        public event EventHandler ShowErrorListChanged;

        [Category("Settings")]
        [DisplayName("Enabled")]
        [Description("Stops the build on the first error")]
        [DefaultValue(true)]
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                var changed = _enabled != value;
                _enabled = value;

                if (changed && EnabledChanged != null)
                    EnabledChanged(this, EventArgs.Empty);
            }
        }

        [Category("Settings")]
        [DisplayName("Show error list")]
        [Description("Show the error list after the build is stopped")]
        [DefaultValue(true)]
        public bool ShowErrorList
        {
            get { return _showErrorList; }
            set
            {
                var changed = _showErrorList != value;
                _showErrorList = value;

                if (changed && ShowErrorListChanged != null)
                    ShowErrorListChanged(this, EventArgs.Empty);
            }
        }
    }
}