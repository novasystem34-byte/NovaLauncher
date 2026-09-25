using System.Windows;

namespace NovaLauncher
{
    public partial class InputDialogWindow : Window
    {
        public string ResultText { get; private set; } = string.Empty;

        public InputDialogWindow(string prompt, string initialValue)
        {
            InitializeComponent();
            PromptText.Text = prompt;
            InputTextBox.Text = initialValue;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            ResultText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
