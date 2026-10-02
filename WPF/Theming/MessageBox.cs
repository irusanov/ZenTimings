using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ZenTimings.Theming
{
    public enum MessageBoxButton
    {
        OK,
        OKCancel,
        YesNoCancel,
        YesNo,
    }

    public enum MessageBoxImage
    {
        None,
        Error,
        Question,
        Warning,
        Information,
    }

    public enum MessageBoxResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No,
        Custom,
    }

    /// <summary>A button of a <see cref="MessageBox"/>.</summary>
    public sealed class MessageBoxButtonModel
    {
        public MessageBoxButtonModel(string label, MessageBoxResult causedResult)
        {
            Label = label;
            CausedResult = causedResult;
        }

        public string Label { get; set; }

        /// <summary>The result of the message box when this button is clicked.</summary>
        public MessageBoxResult CausedResult { get; }

        /// <summary>Identifies a <see cref="MessageBoxResult.Custom"/> button.</summary>
        public object Id { get; set; }

        /// <summary>Clicked by the Enter key.</summary>
        public bool IsDefault { get; set; }

        /// <summary>Clicked by the Escape key.</summary>
        public bool IsCancel { get; set; }
    }

    /// <summary>Factories for the common message box buttons.</summary>
    public static class MessageBoxButtons
    {
        public static IEnumerable<MessageBoxButtonModel> Create(MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.OKCancel: return OkCancel();
                case MessageBoxButton.YesNo: return YesNo();
                case MessageBoxButton.YesNoCancel: return YesNoCancel();
                default: return new[] { Ok() };
            }
        }

        public static IEnumerable<MessageBoxButtonModel> OkCancel(string okLabel = null, string cancelLabel = null) =>
            new[] { Ok(okLabel), Cancel(cancelLabel) };

        public static IEnumerable<MessageBoxButtonModel> YesNo(string yesLabel = null, string noLabel = null) =>
            new[] { Yes(yesLabel), No(noLabel) };

        public static IEnumerable<MessageBoxButtonModel> YesNoCancel(string yesLabel = null, string noLabel = null, string cancelLabel = null) =>
            new[] { Yes(yesLabel), No(noLabel), Cancel(cancelLabel) };

        public static MessageBoxButtonModel Ok(string label = null) =>
            new MessageBoxButtonModel(label ?? "OK", MessageBoxResult.OK) { IsDefault = true };

        public static MessageBoxButtonModel Yes(string label = null) =>
            new MessageBoxButtonModel(label ?? "Yes", MessageBoxResult.Yes) { IsDefault = true };

        public static MessageBoxButtonModel No(string label = null) =>
            new MessageBoxButtonModel(label ?? "No", MessageBoxResult.No);

        public static MessageBoxButtonModel Cancel(string label = null) =>
            new MessageBoxButtonModel(label ?? "Cancel", MessageBoxResult.Cancel) { IsCancel = true };

        public static MessageBoxButtonModel Custom(string label, object id = null) =>
            new MessageBoxButtonModel(label, MessageBoxResult.Custom) { Id = id };
    }

    /// <summary>Content, buttons and, once closed, the result of a <see cref="MessageBox"/>.</summary>
    public sealed class MessageBoxModel
    {
        public string Text { get; set; }

        public string Caption { get; set; }

        public MessageBoxImage Icon { get; set; }

        public IEnumerable<MessageBoxButtonModel> Buttons { get; set; } = new[] { MessageBoxButtons.Ok() };

        public MessageBoxResult Result { get; set; }

        /// <summary>The button that closed the message box, null when it was closed otherwise.</summary>
        public MessageBoxButtonModel ButtonPressed { get; set; }

        /// <summary>Makes the button with the given result the one the Enter key clicks.</summary>
        public void SetDefaultButton(MessageBoxResult defaultResult)
        {
            if (defaultResult == MessageBoxResult.None || Buttons == null)
                return;

            List<MessageBoxButtonModel> buttons = Buttons.ToList();
            if (!buttons.Any(b => b.CausedResult == defaultResult))
                return;

            foreach (MessageBoxButtonModel button in buttons)
                button.IsDefault = button.CausedResult == defaultResult;
        }
    }

    /// <summary>
    /// A message box drawn with the application theme. Mirrors <see cref="System.Windows.MessageBox"/>: it is
    /// modal, owned by the active window, and returns the button that closed it.
    /// </summary>
    public static class MessageBox
    {
        public static MessageBoxResult Show(string text) => Show(new MessageBoxModel { Text = text });

        public static MessageBoxResult Show(
            string text,
            string caption,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return Show(null, text, caption, buttons, icon, defaultResult);
        }

        public static MessageBoxResult Show(
            Window owner,
            string text,
            string caption = null,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            var model = new MessageBoxModel
            {
                Text = text,
                Caption = caption,
                Buttons = MessageBoxButtons.Create(buttons),
                Icon = icon,
            };
            model.SetDefaultButton(defaultResult);
            return Show(owner, model);
        }

        public static MessageBoxResult Show(MessageBoxModel model) => Show(null, model);

        public static MessageBoxResult Show(Window owner, MessageBoxModel model)
        {
            var window = new MessageBoxWindow(model);

            owner = owner ?? ActiveWindow();
            if (owner != null && owner.IsVisible && !ReferenceEquals(owner, window))
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.ShowDialog();
            return model.Result;
        }

        private static Window ActiveWindow() =>
            Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
    }
}
