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
using System.Windows.Shapes;

namespace ClubSiivaWPF
{
    /// <summary>
    /// Interaction logic for TransparentForm.xaml
    /// </summary>
    public partial class TransparentForm : Window
    {
        public Label originallabel = new Label();
        public TransparentForm()
        {
            InitializeComponent();
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }
        private async void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                foreach (MainWindow frm in Application.Current.Windows)
                {
                    if (frm.GetType() == typeof(MainWindow))
                    {
                        await this.Label.Dispatcher.BeginInvoke((Action)(() => Label.Content = originallabel.Content));
                        await this.Label.Dispatcher.BeginInvoke((Action)(() => Label.FontSize = 12));
                        this.Width = 400;
                        break;
                    }
                }
            }
            catch { }
        }
        private void MouseDown_Event(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    this.DragMove();
                }
                e.Handled = true;
            }
            catch
            {
                e.Handled = true;
            }
        }
    }
}
