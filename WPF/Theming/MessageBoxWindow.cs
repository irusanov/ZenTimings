using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZenTimings.Theming
{
    /// <summary>
    /// The window behind <see cref="MessageBox"/>. Its look is defined by its default style in Themes/Generic.xaml;
    /// a theme can restyle it with an implicit style for this type.
    /// </summary>
    public class MessageBoxWindow : ChromeWindow
    {
        public static readonly DependencyProperty MessageIconProperty = DependencyProperty.Register(
            nameof(MessageIcon), typeof(ImageSource), typeof(MessageBoxWindow), new PropertyMetadata(null));

        static MessageBoxWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MessageBoxWindow), new FrameworkPropertyMetadata(typeof(MessageBoxWindow)));
        }

        public MessageBoxWindow(MessageBoxModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            ButtonCommand = new ButtonClickCommand(this);
            DataContext = model;
            Content = model;
            Title = model.Caption ?? string.Empty;
            MessageIcon = GetSystemIcon(model.Icon);

            Closing += OnClosing;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        public MessageBoxModel Model { get; }

        /// <summary>Executed by the buttons with their <see cref="MessageBoxButtonModel"/> as parameter.</summary>
        public ICommand ButtonCommand { get; }

        /// <summary>The system icon that matches <see cref="MessageBoxModel.Icon"/>, null for none.</summary>
        public ImageSource MessageIcon
        {
            get => (ImageSource)GetValue(MessageIconProperty);
            set => SetValue(MessageIconProperty, value);
        }

        private void OnButtonClicked(MessageBoxButtonModel button)
        {
            Model.ButtonPressed = button;
            Model.Result = button.CausedResult;
            DialogResult = button.CausedResult != MessageBoxResult.No && button.CausedResult != MessageBoxResult.Cancel;
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            // Closed with the title bar button, Alt+F4 or Escape.
            if (Model.Result == MessageBoxResult.None)
                Model.Result = GetCancelResult();
        }

        private MessageBoxResult GetCancelResult()
        {
            var buttons = Model.Buttons?.ToList();
            if (buttons == null || buttons.Count == 0)
                return MessageBoxResult.Cancel;

            if (buttons.Any(b => b.CausedResult == MessageBoxResult.Cancel))
                return MessageBoxResult.Cancel;

            if (buttons.Any(b => b.CausedResult == MessageBoxResult.OK))
                return MessageBoxResult.OK;

            return MessageBoxResult.Cancel;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Like the native message box: Escape closes it even without a Cancel button (a button marked
            // IsCancel handles Escape itself), and Ctrl+C copies its caption and text.
            if (e.Key == Key.Escape && !(Model.Buttons?.Any(b => b.IsCancel) ?? false))
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && !(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
            {
                try
                {
                    Clipboard.SetText($"{Model.Caption}{Environment.NewLine}{Environment.NewLine}{Model.Text}");
                }
                catch
                {
                    // The clipboard may be locked by another process; nothing to do.
                }
                e.Handled = true;
            }
        }

        private static ImageSource GetSystemIcon(MessageBoxImage image)
        {
            System.Drawing.Icon icon;
            switch (image)
            {
                case MessageBoxImage.Error: icon = System.Drawing.SystemIcons.Error; break;
                case MessageBoxImage.Question: icon = System.Drawing.SystemIcons.Question; break;
                case MessageBoxImage.Warning: icon = System.Drawing.SystemIcons.Warning; break;
                case MessageBoxImage.Information: icon = System.Drawing.SystemIcons.Information; break;
                default: return null;
            }

            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }

        private sealed class ButtonClickCommand : ICommand
        {
            private readonly MessageBoxWindow window;

            public ButtonClickCommand(MessageBoxWindow window) => this.window = window;

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter) => parameter is MessageBoxButtonModel;

            public void Execute(object parameter)
            {
                if (parameter is MessageBoxButtonModel button)
                    window.OnButtonClicked(button);
            }
        }
    }
}
