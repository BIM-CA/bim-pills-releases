using BIMPills.Core.About;
using BIMPills.UI.Helpers;
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
            SupportEmailText.Text = _info.SupportEmail;
            CopyrightText.Text = _info.Copyright;
        }

        private void Website_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl(_info.Website);

        private void SupportEmail_Click(object sender, MouseButtonEventArgs e)
            => ProcessHelper.OpenUrl($"mailto:{_info.SupportEmail}");

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
