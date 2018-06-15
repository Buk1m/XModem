using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp4.Commands;
using WpfApp4.Model;

namespace WpfApp4.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        #region Private Members

        private Xmodem _senderModem;
        private Xmodem _reciverModem;
        private string _selectedSenderCom;
        private string _selectedReciverCom;
        private string _senderMessage;
        private string _reciverMessage;
        private int _selectedSenderBaudrate = 9600;
        private int _selectedReciverBaudrate = 9600;

        #endregion

        #region Constructor

        public MainViewModel()
        {
            OpenFile = new RelayCommand( () =>
            {
                OpenFileDialog openFileDialog = new OpenFileDialog {Filter = "Text files (*.txt)|*.txt"};
                if (openFileDialog.ShowDialog() != true)
                    return;

                SenderTextBox = File.ReadAllText( openFileDialog.FileName );
            } );

            Send = new RelayCommand( () =>
            {
                if (_senderModem == null || _selectedSenderCom == null)
                {
                    MessageBox.Show( "Choose serial port." );
                    return;
                }

                if (SenderTextBox == null)
                {
                    MessageBox.Show( "Enter message to be sent." );
                    return;
                }

                Task.Run( () => _senderModem.Send( Encoding.ASCII.GetBytes( SenderTextBox + "\r\n" ) ) );
            } );

            BackgroundWorker reciveWorker = new BackgroundWorker();
            reciveWorker.DoWork += ReciveMessage;
            reciveWorker.RunWorkerCompleted += ReciveWorker_RunWorkerCompleted;

            Receive = new RelayCommand( () =>
            {
                if ( _reciverModem == null )
                {
                    MessageBox.Show( "Choose serial port." );
                    return;
                }

                if ( !reciveWorker.IsBusy)
                    reciveWorker.RunWorkerAsync();
            } );

            RefreshPorts = new RelayCommand( () =>
            {
                _senderModem?.Close();
                _reciverModem?.Close();
                _selectedReciverCom = null;
                _selectedSenderCom = null;
                ComPorts?.Clear();
                foreach (var portName in SerialPort.GetPortNames())
                {
                    ComPorts?.Add( portName );
                }
            } );
        }

        private void ReciveWorker_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            ReciverTextBox = File.ReadAllText( @"..\..\message.txt" );
        }

        public void ReciveMessage( object sender, EventArgs e )
        {
            Task a = Task.Run(() => _reciverModem.Receive());
            a.Wait();
        }

        #endregion

        #region Public Properties

        public ObservableCollection<string> ComPorts { get; set; } =
            new ObservableCollection<string>( SerialPort.GetPortNames() );

        public ObservableCollection<string> Baudrate { get; set; } =
            new ObservableCollection<string>() {"9600", "115200"};

        public string SelectedSenderBaudrate
        {
            get => _selectedSenderBaudrate.ToString();
            set
            {
                _selectedSenderBaudrate = int.Parse( value );
                OnPropertyChanged();
            }
        }

        public string SelectedReciverBaudrate
        {
            get => _selectedReciverBaudrate.ToString();
            set
            {
                _selectedReciverBaudrate = int.Parse( value );
                OnPropertyChanged();
            }
        }

        public string SelectedSenderCom
        {
            get => _selectedSenderCom;
            set
            {
                if (_selectedSenderCom == value || value == null )
                    return;

                _selectedSenderCom = value;
                try
                {
                    _senderModem?.Close();
                    _senderModem = new Xmodem( _selectedSenderCom, _selectedSenderBaudrate );
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show( "Port is being used by other process. Acces denied." );
                    OnPropertyChanged();
                }

                OnPropertyChanged();
            }
        }

        public string SelectedReciverCom
        {
            get => _selectedReciverCom;
            set
            {
                if (_selectedReciverCom == value || value == null)
                    return;
                _selectedReciverCom = value;
                try
                {
                    _reciverModem?.Close();
                    _reciverModem = new Xmodem( _selectedReciverCom, _selectedReciverBaudrate );
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show( "Port is being used by other process. Acces denied" );
                    OnPropertyChanged();
                }

                OnPropertyChanged();
            }
        }

        public string SenderTextBox
        {
            get => _senderMessage;

            set
            {
                _senderMessage = value;
                OnPropertyChanged();
            }
        }

        public string ReciverTextBox
        {
            get => _reciverMessage;

            set
            {
                _reciverMessage = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand OpenFile { get; set; }
        public ICommand Send { get; set; }
        public ICommand Receive { get; set; }
        public ICommand RefreshPorts { get; set; }

        #endregion
    }
}