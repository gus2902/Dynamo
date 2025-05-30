using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using Dynamo.Core;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using Dynamo.UI.Commands;
using Dynamo.ViewModels;
using Dynamo.Wpf.Properties;
using Dynamo.Wpf.ViewModels.Core;

namespace Dynamo.Wpf.ViewModels
{
    /// <summary>
    /// The RunTypeItem class wraps a RunType for display in the ComboBox.
    /// </summary>
    public class RunTypeItem : NotificationObject
    {
        private bool enabled;
        private bool isSelected;

        /// <summary>
        /// The enabled flag sets whether the RunType is selectable
        /// in the view.
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set
            {
                enabled = value;
                RaisePropertyChanged("Enabled");
                RaisePropertyChanged("ToolTipText");
            }
        }

        /// <summary>
        /// If this RunTypeItem is selected
        /// </summary>
        public bool IsSelected
        {
            get => isSelected;
            internal set
            {
                isSelected = value;
                RaisePropertyChanged(nameof(IsSelected));
            }
        }

        public RunType RunType { get; set; }

        public string Name
        {
            get { return Resources.ResourceManager.GetString(RunType.ToString()); }
        }

        public string ToolTipText
        {
            get
            {
                switch (RunType)
                {
                    case RunType.Automatic:
                        return Resources.RunTypeToolTipAutomatically;
                    case RunType.Manual:
                        return Resources.RunTypeToolTipManually;
                    case RunType.Periodic:
                        return enabled
                            ? Resources.RunTypeToolTipPeriodicallyEnabled
                            : Resources.RunTypeToolTipPeriodicallyDisabled;
                    default: 
                        return string.Empty;
                }
            }
        }

        public RunTypeItem(RunType runType)
        {
            RunType = runType;
            Enabled = true;
        }
    
    }

    /// <summary>
    /// The RunSettingsViewModel is the view model for the 
    /// RunSettings object on a given HomeWorkspaceModel. This class
    /// handles property change notification from the underlying RunSettings
    /// object, raising corresponding property change notifications. Those
    /// property change notifications are, in turn, handled by the WorkspaceViewModel.
    /// Setters on the properties in this class do not raise property change
    /// notifications as those notifications are raised when the value is set on the
    /// model.
    /// </summary>
    public class RunSettingsViewModel : ViewModelBase
    {
        #region private members

        private bool debug = false;
        private HomeWorkspaceViewModel workspaceViewModel;
        private readonly DynamoViewModel dynamoViewModel;
        private RunTypeItem selectedRunTypeItem;
        private SynchronizationContext context;

        #endregion

        #region properties

        public RunSettings Model { get; private set; }

        public int RunPeriod
        {
            get { return Model.RunPeriod; }
            set
            {
                Model.RunPeriod = value;
            }
        }

        public Visibility RunPeriodInputVisibility
        {
            get
            {
                // When switching the run type, also
                // set the run period input visibility
                switch (SelectedRunTypeItem.RunType)
                {
                    case RunType.Manual:
                    case RunType.Automatic:
                        return Visibility.Collapsed;
                    case RunType.Periodic:
                        return Visibility.Visible;
                    default:
                        return Visibility.Hidden;
                }
            }

        }

        public bool RunEnabled
        {
            get { return Model.RunEnabled; }
        }

        public bool RunButtonEnabled
        {
            get
            {
                return Model.RunEnabled && // Running graphs is enabled
                    !(workspaceViewModel.Model as HomeWorkspaceModel).GraphRunInProgress && // Not during graph execution
                    Model.RunType == RunType.Manual; // Is in manual mode
            }
        }

        /// <summary>
        /// This value will enable/disable the RunType ComboBox located in RunSettingsControl.xaml
        /// </summary>
        public bool RunTypesEnabled
        {
            get
            {
                return Model.RunTypesEnabled;
            }
        }


        public string RunButtonToolTip
        {
            get
            {
                if (RunButtonEnabled == false && RunTypesEnabled == false)
                    return Resources.DynamoViewRunButtonToolTipDisabledFileTrust;
                else
                {
                    return RunButtonEnabled
                    ? Resources.DynamoViewRunButtonTooltip
                    : Resources.DynamoViewRunButtonToolTipDisabled;
                }              
            }
        }

        public string RunTypesComboBoxToolTip
        {
            get
            {
                return RunTypesEnabled
                    ? Resources.DynamoViewRunTypesComboBoxToolTipEnabled.Replace("\\n", System.Environment.NewLine)
                    : Resources.DynamoViewRunButtonToolTipDisabled;
            }
        }

        public virtual bool RunInDebug
        {
            get { return debug; }
            set
            {
                debug = value;

                if (debug)
                {
                    Model.RunType = RunType.Manual;
                    ToggleRunTypeEnabled(RunType.Automatic, false);
                    ToggleRunTypeEnabled(RunType.Periodic, false);
                }
                else
                {
                    ToggleRunTypeEnabled(RunType.Automatic, true);
                    ToggleRunTypeEnabled(RunType.Periodic, true);
                    workspaceViewModel.CheckAndSetPeriodicRunCapability();
                }

                RaisePropertyChanged("RunInDebug");
            }
        }

        public RunTypeItem SelectedRunTypeItem
        {
            get { return RunTypeItems.First(rt => rt.RunType == Model.RunType); }
            set
            {
                selectedRunTypeItem = value;
                Model.RunType = selectedRunTypeItem.RunType;
                RunTypeItems.ToList().ForEach(x => x.IsSelected = false);
                selectedRunTypeItem.IsSelected = true;
            }
        }

        public ObservableCollection<RunTypeItem> RunTypeItems { get; set; }
 
        public DelegateCommand RunExpressionCommand { get; private set; }

        public DelegateCommand CancelRunCommand { get; set; }

        public Visibility DebugCheckBoxVisibility
        {
            get
            {
#if DEBUG
                return Visibility.Visible;
#else
                return Visibility.Collapsed;
#endif
            }
        }

        public Visibility RunButtonVisibility
        {
            get
            {
                return Model.RunType == RunType.Manual
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region constructors and dispose function

        public RunSettingsViewModel(RunSettings settings, HomeWorkspaceViewModel workspaceViewModel, DynamoViewModel dynamoViewModel)
        {
            Model = settings;
            Model.PropertyChanged += Model_PropertyChanged;

            this.workspaceViewModel = workspaceViewModel;
            workspaceViewModel.Model.PropertyChanged += HomeWorkspaceModel_PropertyChanged;

            this.dynamoViewModel = dynamoViewModel;

            CancelRunCommand = new DelegateCommand(CancelRun, CanCancelRun);
            RunExpressionCommand = new DelegateCommand(RunExpression, CanRunExpression);

            RunTypeItems = new ObservableCollection<RunTypeItem>();
            foreach (RunType val in Enum.GetValues(typeof(RunType)))
            {
                RunTypeItems.Add(new RunTypeItem(val));
            }
            ToggleRunTypeEnabled(RunType.Periodic, false);
        }

        /// <summary>
        /// When switching workspace, this need to be called in HomeworkspaceViewModel dispose function
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();

            if (Model != null)
            {
                Model.PropertyChanged -= Model_PropertyChanged;
            }

            if (workspaceViewModel != null && workspaceViewModel.Model != null)
            {
                workspaceViewModel.Model.PropertyChanged -= HomeWorkspaceModel_PropertyChanged;
            }
            
            workspaceViewModel = null;
        }

        #endregion

        #region private and internal methods

        /// <summary>
        /// Notifies all relevant Dynamo features (UI elements, commands) that the Graph execution has been enabled/disabled. 
        /// </summary>
        void NotifyOfGraphRunChanged()
        {
            // Skip UI updates during periodic mode to prevent unnecessary toggling of UI elements
            if (Model.RunType == RunType.Periodic) return;

            RaisePropertyChanged(nameof(RunButtonEnabled));
            RaisePropertyChanged(nameof(RunButtonToolTip));

            if (string.IsNullOrEmpty(DynamoModel.HostAnalyticsInfo.HostName))
            {
                Application.Current?.Dispatcher.Invoke(new Action(() =>
                {
                    dynamoViewModel.ShowOpenDialogAndOpenResultCommand.RaiseCanExecuteChanged();
                    dynamoViewModel.NewHomeWorkspaceCommand.RaiseCanExecuteChanged();
                    dynamoViewModel.OpenRecentCommand.RaiseCanExecuteChanged();
                    dynamoViewModel.CloseHomeWorkspaceCommand.RaiseCanExecuteChanged();
                }));
            }
        }

        /// <summary>
        /// Called when the RunSettings model has property changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Model_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(RunSettings.RunEnabled):
                    RaisePropertyChanged("RunEnabled");
                    NotifyOfGraphRunChanged();
                    break;
                case "RunPeriod":
                case "RunType":
                    RaisePropertyChanged("RunPeriod");
                    RaisePropertyChanged("RunEnabled");
                    RaisePropertyChanged("RunButtonToolTip");
                    RaisePropertyChanged("RunPeriodInputVisibility");
                    RaisePropertyChanged("RunButtonEnabled");
                    RaisePropertyChanged("RunTypeItems");
                    RaisePropertyChanged("SelectedRunTypeItem");
                    RaisePropertyChanged("RunButtonVisibility");
                    RunTypeChangedRun(null);
                    break;
                case "RunTypesEnabled":
                    RaisePropertyChanged("RunTypesEnabled");
                    break;
                case "RunTypesComboBoxToolTipIsEnabled":
                    RaisePropertyChanged("RunTypesComboBoxToolTip");
                    break;
            }
        }

        void HomeWorkspaceModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(HomeWorkspaceModel.GraphRunInProgress):
                    NotifyOfGraphRunChanged();
                    break;
            }
        }

        private void RunTypeChangedRun(object obj)
        {
            workspaceViewModel.StopPeriodicTimerCommand.Execute(null);
            switch (Model.RunType)
            {
                case RunType.Manual:                    
                    return;
                case RunType.Automatic:
                    RunExpressionCommand.Execute(true);
                    return;
                case RunType.Periodic:
                    dynamoViewModel.ShowRunPreview = false;
                    workspaceViewModel.StartPeriodicTimerCommand.Execute(null);
                    return;
            }
        }

        private void RunExpression(object parameters)
        {
            bool displayErrors = Convert.ToBoolean(parameters);
            var command = new DynamoModel.RunCancelCommand(displayErrors, false);
            dynamoViewModel.ExecuteCommand(command);
        }

        internal static bool CanRunExpression(object parameters)
        {
            return true;
        }

        private void CancelRun(object parameter)
        {
            var command = new DynamoModel.RunCancelCommand(false, true);
            dynamoViewModel.ExecuteCommand(command);
        }

        private static bool CanCancelRun(object parameter)
        {
            return true;
        }

        internal void ToggleRunTypeEnabled(RunType runType, bool enabled)
        {
            var prt = RunTypeItems.First(rt => rt.RunType == runType);
            prt.Enabled = enabled;
        }

        #endregion

    }

    /// <summary>
    /// The RunPeriodConverter converts input text to and from an integer
    /// value with a trailing "ms".
    /// </summary>
    public class RunPeriodConverter : IValueConverter
    {
        public const string ExpectedSuffix = "ms";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Format("{0}{1}", value, ExpectedSuffix);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value.ToString();

            if (s.EndsWith(ExpectedSuffix))
            {
                s = s.Remove(s.Length - ExpectedSuffix.Length);
            }
            
            int ms;
            var parseSuccess =
                Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);

            return parseSuccess ? Math.Abs(ms) : RunSettings.DefaultRunPeriod;
        }
    }
}
