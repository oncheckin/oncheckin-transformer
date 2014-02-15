using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;

namespace OnCheckinTransforms.VisualStudio
{
    /// <summary>
    /// Interaction logic for EnvironmentEntry.xaml
    /// </summary>
    public partial class EnvironmentEntry : Microsoft.VisualStudio.PlatformUI.DialogWindow
    {
        public EnvironmentEntry()
        {
            InitializeComponent();
            this.HasMaximizeButton = false;
            this.HasMinimizeButton = false;
        }
        
        public string InputText { get; set; }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput(input.Text))
            {
                System.Windows.MessageBox.Show("Your environment name contains invalid characters.");
                return;
            }
            InputText = input.Text.Replace(Environment.NewLine,string.Empty);
            this.Close();
        }

        private bool ValidateInput(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length < 3) return false;
            var regex = new Regex(@"^[a-zA-Z\d\-_\s]+$");
            return regex.IsMatch(input);
        }

        private void input_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnOk_Click(sender, e);
            }
        }
    }
}
