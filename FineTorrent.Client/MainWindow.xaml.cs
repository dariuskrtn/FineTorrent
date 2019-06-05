using FineTorrent.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FineTorrent.Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new Thread(_ =>
            {
                Torrent torrent = null;
                this.Dispatcher.Invoke(() =>
                {
                    torrent = new Torrent(filePath.Text, downloadPath.Text);
                });
                torrent.GetProgressObservable().Subscribe(percent =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = percent;
                    });
                });
                torrent.StartDownload().ConfigureAwait(false).GetAwaiter().GetResult();

                this.Dispatcher.Invoke(() =>
                {
                    name.Content = torrent.GetTorrentFile().Name;
                    size.Content = torrent.GetTorrentFile().Size;
                });

            }).Start();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                this.filePath.Text = openFileDialog.FileName;
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
    }
}
