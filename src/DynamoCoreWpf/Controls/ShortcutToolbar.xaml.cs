using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dynamo.UI.Commands;
using Dynamo.ViewModels;
using Dynamo.Wpf.ViewModels.Core;
using NotificationObject = Dynamo.Core.NotificationObject;
using Greg.AuthProviders;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using System;

namespace Dynamo.UI.Controls
{
    /// <summary>
    /// An object which provides the data for the shortcut toolbar.
    /// </summary>
    public partial class ShortcutToolbar : UserControl, IDisposable
    {
        private readonly ObservableCollection<ShortcutBarItem> shortcutBarItems;
        private readonly ObservableCollection<ShortcutBarItem> shortcutBarRightSideItems;

        /// <summary>
        /// A collection of <see cref="ShortcutBarItem"/>.
        /// </summary>
        public ObservableCollection<ShortcutBarItem> ShortcutBarItems
        {
            get { return shortcutBarItems; }
        }

        /// <summary>
        /// A collection of <see cref="ShortcutBarItems"/> for the right hand side of the shortcut bar.
        /// </summary>
        public ObservableCollection<ShortcutBarItem> ShortcutBarRightSideItems
        {
            get { return shortcutBarRightSideItems; }
        }
        private readonly Core.AuthenticationManager authManager;
        public readonly DynamoViewModel DynamoViewModel;

        /// <summary>
        /// Construct a ShortcutToolbar.
        /// </summary>
        /// <param name="dynamoViewModel"></param>
        public ShortcutToolbar(DynamoViewModel dynamoViewModel)
        {
            DynamoViewModel = dynamoViewModel;
            shortcutBarItems = new ObservableCollection<ShortcutBarItem>();
            shortcutBarRightSideItems = new ObservableCollection<ShortcutBarItem>();

            InitializeComponent();

            var shortcutToolbar = new ShortcutToolbarViewModel(dynamoViewModel);
            DataContext = shortcutToolbar;
            authManager = dynamoViewModel.Model.AuthenticationManager;
            if (authManager != null)
            {
                authManager.LoginStateChanged += AuthChangeHandler;
                if (authManager.LoginState == LoginState.LoggedIn)
                {
                    if (loginMenu.Items.Count == 0)
                    {
                        loginMenu.Items.Add(logoutOption);
                    }
                }
                else
                {
                    loginMenu.Items.Remove(logoutOption);
                }
            }

            this.Loaded += ShortcutToolbar_Loaded;
        }

        private void ShortcutToolbar_Loaded(object sender, RoutedEventArgs e)
        {
            IsSaveButtonEnabled = false;
            IsExportMenuEnabled = false;
            DynamoViewModel.OnRequestShorcutToolbarLoaded(RightMenu.ActualWidth);
        }

        public void Dispose()
        {
            if(authManager != null)
            {
                authManager.LoginStateChanged -= AuthChangeHandler;
            }
            this.Loaded -= ShortcutToolbar_Loaded;
        }

        private void AuthChangeHandler(LoginState status)
        {
            if (status == LoginState.LoggedOut)
            {
                var toolTip = new ToolTip
                {
                    Content = Wpf.Properties.Resources.SignInButtonContentToolTip
                };

                // Retrieve the style from resources
                var style = (Style)TryFindResource("GenericToolTipLight");
                if (style != null)
                {
                    toolTip.Style = style;
                }
                else
                {
                    // Optionally log a warning or handle the missing resource gracefully
                    System.Diagnostics.Debug.WriteLine("Warning: 'GenericToolTipLight' resource not found.");
                }

                // Assign the styled tooltip to the Login Button
                LoginButton.ToolTip = toolTip;

                txtSignIn.Text = Wpf.Properties.Resources.SignInButtonText;
                logoutOption.Visibility = Visibility.Collapsed;
                loginMenu.Items.Remove(logoutOption);
            }
            else if (status == LoginState.LoggedIn)
            {
                txtSignIn.Text = authManager.Username;
                LoginButton.ToolTip = null;
                if (loginMenu.Items.Count == 0)
                {
                    loginMenu.Items.Add(logoutOption);
                }
                logoutOption.Visibility = Visibility.Visible;
            }
        }

        private void exportMenu_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.HeaderText.FontFamily = SharedDictionaryManager.DynamoModernDictionary["ArtifaktElementRegular"] as FontFamily;
            this.Icon.Source = new BitmapImage(new System.Uri(@"pack://application:,,,/DynamoCoreWpf;component/UI/Images/image-icon.png"));
        }

        private void exportMenu_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.HeaderText.FontFamily = SharedDictionaryManager.DynamoModernDictionary["ArtifaktElementRegular"] as FontFamily;
            this.Icon.Source = new BitmapImage(new System.Uri(@"pack://application:,,,/DynamoCoreWpf;component/UI/Images/image-icon-default.png"));
        }

        private void LoginButton_OnClick(object sender, RoutedEventArgs e)
        {
            if(!DynamoViewModel.IsIDSDKInitialized()) return;
            if (authManager.LoginState == LoginState.LoggedIn)
            {
                var button = (Button)sender;
                MenuItem mi = button.Parent as MenuItem;
                if (mi != null)
                {
                    mi.IsSubmenuOpen = !mi.IsSubmenuOpen;
                }
            }
            else if (authManager.LoginState == LoginState.LoggedOut)
            {
                authManager.ToggleLoginState(null);
                if (authManager.IsLoggedIn()) {
                    var tb = (((sender as Button).Content as StackPanel).Children.OfType<TextBlock>().FirstOrDefault() as TextBlock);
                    tb.Text = authManager.Username;
                    logoutOption.Visibility = Visibility.Visible;
                    LoginButton.ToolTip = null;
                }
            }
        }

        private void LogoutOption_Click(object sender, RoutedEventArgs e)
        {
            if (!DynamoViewModel.IsIDSDKInitialized()) return;
            if (authManager.LoginState == LoginState.LoggedIn)
            {
                var result = Wpf.Utilities.MessageBoxService.Show(Application.Current?.MainWindow, Wpf.Properties.Resources.SignOutConfirmationDialogText, Wpf.Properties.Resources.SignOutConfirmationDialogTitle,MessageBoxButton.OKCancel, new List<string>() { "Sign Out", "Cancel"}, MessageBoxImage.Information);
                if (result == MessageBoxResult.OK)
                {
                    authManager.ToggleLoginState(null);
                }
            }
        }

        public List<Control> AllChildren(DependencyObject parent)
        {
            var _list = new List<Control> { };
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var _child = VisualTreeHelper.GetChild(parent, i);
                if (_child is Control)
                {
                    _list.Add(_child as Control);
                    _list.AddRange(AllChildren(_child));
                }
            }
            return _list;
        }

        private Button GetButton(string shortcutName)
        {
            if (this.shortcutBarItems.Count > 1)
            {
                try
                {
                    int buttonIndex = ShortcutBarItems.ToList().FindIndex(item => item.Name.ToUpper() == shortcutName);
                    var _container = ShortcutItemsControl.ItemContainerGenerator.ContainerFromIndex(buttonIndex);
                    var _children = AllChildren(_container);
                    var _control = (Button)_children.First();
                    return _control;
                }
                catch (System.Exception)
                {
                    return null;
                }                
            }
            else
            {
                return null;
            }
        }

        internal bool IsNewButtonEnabled
        {
            set
            {
                Button saveButton = GetButton("NEW");
                if (saveButton != null)
                {
                    saveButton.IsEnabled = value;
                    saveButton.Opacity = value ? 1 : 0.5;
                }
            }
        }

        internal bool IsOpenButtonEnabled
        {
            set
            {
                Button saveButton = GetButton("OPEN");
                if (saveButton != null)
                {
                    saveButton.IsEnabled = value;
                    saveButton.Opacity = value ? 1 : 0.5;
                }
            }
        }

        internal bool IsSaveButtonEnabled
        {
            set
            {
                Button saveButton = GetButton("SAVE");
                if (saveButton != null)
                {
                    saveButton.IsEnabled = value;
                    saveButton.Opacity = value ? 1 : 0.5;
                }
            }
        }

        internal bool IsLoginMenuEnabled
        {
            set
            {
                this.loginMenu.IsEnabled = value;
                this.loginMenu.Opacity = value ? 1 : 0.5;
            }
        }

        internal bool IsExportMenuEnabled
        {
            set
            {
                this.exportMenu.IsEnabled = value;
                this.exportMenu.Opacity = value ? 1 : 0.5;
            }
        }

        internal bool IsNotificationCenterEnabled
        {
            set
            {
                this.NotificationCenter.IsEnabled = value;
                this.NotificationCenter.Opacity = value ? 1 : 0.5;
            }
        }
    }

    /// <summary>
    /// An object which provides the data for an item in the shortcut toolbar.
    /// </summary>
    public partial class ShortcutBarItem : NotificationObject
    {
        protected string shortcutToolTip;

        /// <summary>
        /// A parameter sent to the ShortcutCommand.
        /// </summary>
        public string ShortcutCommandParameter { get; set; }

        /// <summary>
        /// The Command that will be executed by this shortcut item.
        /// </summary>
        public DelegateCommand ShortcutCommand { get; set; }

        /// <summary>
        /// The path to the image for the disabled state of this shortcut item.
        /// </summary>
        public string ImgDisabledSource { get; set; }

        /// <summary>
        /// The path to the image for the hover state of this shortcut item.
        /// </summary>
        public string ImgHoverSource { get; set; }

        /// <summary>
        /// The path to the image for the normal state of this shortcut item.
        /// </summary>
        public string ImgNormalSource { get; set; }

        /// <summary>
        /// The tooltip for this shortcut item.
        /// </summary>
        public virtual string ShortcutToolTip
        {
            get { return shortcutToolTip; }
            set { shortcutToolTip = value; }
        }

        /// <summary>
        /// The Name of the shortcut
        /// </summary>
        public string Name { get; set; }
    }

    internal partial class ImageExportShortcutBarItem : ShortcutBarItem
    {
        private readonly DynamoViewModel vm;

        public override string ShortcutToolTip
        {
            get
            {
                return vm.BackgroundPreviewViewModel == null || !vm.BackgroundPreviewViewModel.CanNavigateBackground
                    ? Wpf.Properties.Resources.DynamoViewToolbarExportButtonTooltip
                    : Wpf.Properties.Resources.DynamoViewToolbarExport3DButtonTooltip;
            }
            set
            {
                shortcutToolTip = value;
            }
        }

        public ImageExportShortcutBarItem(DynamoViewModel viewModel)
        {
            vm = viewModel;
            vm.BackgroundPreviewViewModel.PropertyChanged += BackgroundPreviewViewModel_PropertyChanged;
        }

        private void BackgroundPreviewViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "CanNavigateBackground") return;
            RaisePropertyChanged("ShortcutToolTip");
        }
    }
}
