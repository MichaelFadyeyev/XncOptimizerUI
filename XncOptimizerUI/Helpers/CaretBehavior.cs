using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Attached behavior for a <see cref="TextBox"/> whose view model rewrites the bound text on
    /// the fly (","->".", trailing-separator trim, ".5"->"0.5"). When that happens while the box
    /// is focused the TwoWay binding resets the caret to the start; <c>KeepAtEndOnRewrite</c> puts
    /// it back at the end so the next keystroke appends. Only acts when the caret was actually
    /// reset to 0, so ordinary left-to-right typing and mid-text edits are untouched.
    /// </summary>
    public static class CaretBehavior
    {
        public static readonly DependencyProperty KeepAtEndOnRewriteProperty = DependencyProperty.RegisterAttached(
            "KeepAtEndOnRewrite", typeof(bool), typeof(CaretBehavior),
            new PropertyMetadata(false, OnKeepAtEndOnRewriteChanged));

        public static bool GetKeepAtEndOnRewrite(DependencyObject element) => (bool)element.GetValue(KeepAtEndOnRewriteProperty);
        public static void SetKeepAtEndOnRewrite(DependencyObject element, bool value) => element.SetValue(KeepAtEndOnRewriteProperty, value);

        private static void OnKeepAtEndOnRewriteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            textBox.TextChanged -= TextBox_TextChanged;

            if (e.NewValue is true)
            {
                textBox.TextChanged += TextBox_TextChanged;
            }
        }

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;

            textBox.Dispatcher.BeginInvoke(() =>
            {
                if (textBox.IsKeyboardFocused && textBox.CaretIndex == 0 && textBox.Text.Length > 0)
                {
                    textBox.CaretIndex = textBox.Text.Length;
                }
            }, DispatcherPriority.Background);
        }
    }
}
