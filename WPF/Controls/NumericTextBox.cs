using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZenTimings.Controls
{
    public class NumericTextBox : TextBox
    {
        private static readonly Regex InvalidCharacters =
            new Regex("[^0-9]");

        private bool _updatingText;
        private bool _updatingValue;

        public NumericTextBox()
        {
            DataObject.AddPastingHandler(this, OnPasting);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            if (Style == null)
            {
                Style = FindResource(typeof(TextBox)) as Style;
            }
        }

        #region Minimum

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(int),
                typeof(NumericTextBox),
                new FrameworkPropertyMetadata(0, OnRangeChanged));

        public int Minimum
        {
            get { return (int)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        #endregion

        #region Maximum

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(int),
                typeof(NumericTextBox),
                new FrameworkPropertyMetadata(
                    int.MaxValue,
                    OnRangeChanged));

        public int Maximum
        {
            get { return (int)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        #endregion

        #region Value

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(int?),
                typeof(NumericTextBox),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged));

        public int? Value
        {
            get { return (int?)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        #endregion

        #region AllowEmpty

        public static readonly DependencyProperty AllowEmptyProperty =
            DependencyProperty.Register(
                nameof(AllowEmpty),
                typeof(bool),
                typeof(NumericTextBox),
                new FrameworkPropertyMetadata(true, OnAllowEmptyChanged));

        public bool AllowEmpty
        {
            get { return (bool)GetValue(AllowEmptyProperty); }
            set { SetValue(AllowEmptyProperty, value); }
        }

        #endregion

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
            {
                base.OnPreviewTextInput(e);
                return;
            }

            for (int i = 0; i < e.Text.Length; i++)
            {
                if (e.Text[i] < '0' || e.Text[i] > '9')
                {
                    e.Handled = true;
                    return;
                }
            }

            string newText = GetTextAfterInput(e.Text);

            if (!IsValidText(newText))
            {
                e.Handled = true;
                return;
            }

            base.OnPreviewTextInput(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Back:
                case Key.Delete:
                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                case Key.Home:
                case Key.End:
                case Key.Tab:
                case Key.Enter:
                case Key.Escape:
                    base.OnPreviewKeyDown(e);
                    return;
            }

            base.OnPreviewKeyDown(e);
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            if (_updatingText)
            {
                base.OnTextChanged(e);
                return;
            }

            string text = Text;

            string cleanedText =
                InvalidCharacters.Replace(text, "");

            if (cleanedText != text)
            {
                int selectionStart = SelectionStart;

                _updatingText = true;

                Text = cleanedText;

                if (selectionStart > Text.Length)
                    selectionStart = Text.Length;

                SelectionStart = selectionStart;

                _updatingText = false;
            }

            UpdateValueFromText();

            base.OnTextChanged(e);
        }

        private void OnPasting(
            object sender,
            DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }

            string pastedText =
                (string)e.DataObject.GetData(typeof(string));

            if (string.IsNullOrEmpty(pastedText))
            {
                e.CancelCommand();
                return;
            }

            string newText = GetTextAfterInput(pastedText);

            if (!IsValidText(newText))
            {
                e.CancelCommand();
            }
        }

        private string GetTextAfterInput(string input)
        {
            string text = Text ?? string.Empty;

            int start = SelectionStart;
            int length = SelectionLength;

            if (start < 0)
                start = 0;

            if (start > text.Length)
                start = text.Length;

            if (length < 0)
                length = 0;

            if (start + length > text.Length)
                length = text.Length - start;

            return text
                .Remove(start, length)
                .Insert(start, input);
        }

        private bool IsValidText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return AllowEmpty;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                    return false;
            }

            int value;

            if (!int.TryParse(text, out value))
                return false;

            return value >= Minimum && value <= Maximum;
        }

        private void UpdateValueFromText()
        {
            if (_updatingValue)
                return;

            if (string.IsNullOrEmpty(Text))
            {
                if (AllowEmpty)
                {
                    _updatingValue = true;
                    Value = null;
                    _updatingValue = false;
                }
                else
                {
                    SetTextInternal(Minimum.ToString());

                    _updatingValue = true;
                    Value = Minimum;
                    _updatingValue = false;
                }

                return;
            }

            int value;

            if (!int.TryParse(Text, out value))
                return;

            if (value < Minimum)
                value = Minimum;

            if (value > Maximum)
                value = Maximum;

            if (Value != value)
            {
                _updatingValue = true;
                Value = value;
                _updatingValue = false;
            }
        }

        private static void OnValueChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            NumericTextBox control = (NumericTextBox)d;

            if (control._updatingValue)
                return;

            int? value = (int?)e.NewValue;

            if (!value.HasValue)
            {
                if (control.AllowEmpty)
                    control.SetTextInternal(string.Empty);

                return;
            }

            int clampedValue = value.Value;

            if (clampedValue < control.Minimum)
                clampedValue = control.Minimum;

            if (clampedValue > control.Maximum)
                clampedValue = control.Maximum;

            if (clampedValue != value.Value)
            {
                control._updatingValue = true;
                control.Value = clampedValue;
                control._updatingValue = false;
            }

            control.SetTextInternal(clampedValue.ToString());
        }

        private static void OnRangeChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            NumericTextBox control = (NumericTextBox)d;

            if (control.Minimum > control.Maximum)
            {
                if (e.Property == MinimumProperty)
                    control.Maximum = control.Minimum;
                else
                    control.Minimum = control.Maximum;
            }

            if (control.Value.HasValue)
            {
                int value = control.Value.Value;

                if (value < control.Minimum)
                    value = control.Minimum;

                if (value > control.Maximum)
                    value = control.Maximum;

                if (value != control.Value.Value)
                    control.Value = value;
            }

            control.UpdateValueFromText();
        }

        private static void OnAllowEmptyChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            NumericTextBox control = (NumericTextBox)d;

            if (!control.AllowEmpty &&
                string.IsNullOrEmpty(control.Text))
            {
                control.Value = control.Minimum;
            }
        }

        private void SetTextInternal(string text)
        {
            if (Text == text)
                return;

            _updatingText = true;

            Text = text;
            SelectionStart = Text.Length;

            _updatingText = false;
        }
    }
}
