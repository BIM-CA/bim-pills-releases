using BIMPills.Core.About;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace BIMPills.UI.About
{
    public partial class AboutWindow : Window
    {
        private readonly AboutInfo _info;

        public AboutWindow(AboutInfo info)
        {
            _info = info;
            InitializeComponent();
            Populate();
        }

        private void Populate()
        {
            DescriptionText.Text = _info.Description;
            VersionText.Text = _info.Version;
            DeveloperText.Text = _info.Developer;
            CompanyText.Text = _info.Company;
            WebsiteText.Text = _info.Website;
            CopyrightText.Text = _info.Copyright;
        }

        private void Website_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _info.Website,
                UseShellExecute = true
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
