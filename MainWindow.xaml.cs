using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FingersApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Fingers fingers;

        TextBlock leapStatus;
        TextBlock leftRingStatus;
        TextBlock rightRingStatus;
        ComboBox leapProfileSelector;

        public MainWindow()
        {
            InitializeComponent();
            leapStatus = (TextBlock)this.FindName("LeapStatus");
            leftRingStatus = (TextBlock)this.FindName("LeftRingStatus");
            rightRingStatus = (TextBlock)this.FindName("RightRingStatus");
            leapProfileSelector = (ComboBox)this.FindName("LeapProfileSelector");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            fingers = new Fingers(this);
        }

        public void SetLeapStatus(String status)
        {
            leapStatus.Text = status;
        }
        public void SetLeftRingStatus(String status)
        {
            leftRingStatus.Text = status.Equals("0") ? "Connecting..." : status;
        }
        public void SetRightRingStatus(String status)
        {
            rightRingStatus.Text = status.Equals("0") ? "Connecting..." : status;
        }

        public void SelectLeapProfile(String name)
        {
            leapProfileSelector.SelectedIndex =
                leapProfileSelector.Items.Cast<ComboBoxItem>()
                    .Select(c => (string)c.Content)
                    .ToList()
                    .IndexOf(name);
        }

        private void Swap(object sender, RoutedEventArgs e)
        {
            fingers.SwapRings();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            String text = (e.AddedItems[0] as ComboBoxItem).Content.ToString();

            // If fingers is null this is getting called from fingers' constructor as it
            // loads the previous data, so we don't need/want to call back to it
            if (text != null && fingers != null)
                fingers.SetLeapProfile(text, false);
        }
    }
}
