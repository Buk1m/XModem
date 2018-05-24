using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Windows;

namespace WpfApp4.Model
{
    public class Xmodem
    {
        private const int SOH = 1;
        private const int EOT = 4;
        private const int ACK = 6;
        private const int NAK = 21;
        private const int SUB = 26;

        private readonly SerialPort portNumber;

        public string Message { get; set; }

        public Xmodem( string portName, int baudrate )
        {
            portNumber = new SerialPort( portName ) {ReadTimeout = 10000, BaudRate = baudrate};
            portNumber.Open();
        }

        public void Close()
        {
            portNumber.Close();
        }

        public void Send( byte[] bytesToSend )
        {
            try
            {
                if (!portNumber.IsOpen)
                    portNumber.Open();
                using (portNumber)
                {
                    while (Convert.ToInt32( portNumber.ReadLine() ) != NAK) ;

                    for (int i = 0; i <= bytesToSend.Length / 128; i++)
                    {
                        do
                        {
                            portNumber.WriteLine( SOH.ToString() );
                            portNumber.WriteLine( i.ToString() );
                            portNumber.WriteLine( ( 255 - i ).ToString() );
                            for (int j = 0; j < 128; j++)
                            {
                                if (i * 128 + j == bytesToSend.Length)
                                {
                                    portNumber.Write( Convert.ToChar( SUB ).ToString() );
                                    break;
                                }

                                portNumber.Write( Convert.ToChar( bytesToSend[i * 128 + j] ).ToString() );

                            }

                            byte[] crc = createCRC( bytesToSend );
                            for (int j = 0; j < 2; j++)
                            {
                                portNumber.Write( Convert.ToChar( crc[j] ).ToString() );
                            }

                        } while (Convert.ToInt32( portNumber.ReadLine() ) == NAK);
                    }

                    do
                    {
                        portNumber.WriteLine( EOT.ToString() );
                    } while (Convert.ToInt32( portNumber.ReadLine() ) != ACK);
                }
            }
            catch (Exception ex)
            {
                if (ex is InvalidOperationException || ex is TimeoutException || ex is IOException)
                {
                    MessageBox.Show( ex.Message );
                    return;
                }
                throw;
            }
        }

        public void Receive()
        {
            try
            {
                if (!portNumber.IsOpen)
                    portNumber.Open();
                using (portNumber)
                {
                    List<byte> tmpByteList = new List<byte>();
                    List<byte> byteList = new List<byte>();

                    do
                    {
                        portNumber.WriteLine( NAK.ToString() );
                    } while (Convert.ToInt32( portNumber.ReadLine() ) != SOH);

                    bool error = false;
                    do
                    {
                        if (255 - Convert.ToInt32( portNumber.ReadLine() ) != Convert.ToInt32( portNumber.ReadLine() ))
                            error = true;
                        else
                        {
                            for (int i = 0; i < 128; i++)
                            {
                                int tmp = portNumber.ReadChar();
                                if (tmp == SUB)
                                    break;
                                else
                                    tmpByteList.Add( Convert.ToByte( tmp ) );
                            }

                            bool ifDataCorrect = true;
                            byte[] crc = createCRC( tmpByteList.ToArray() );
                            for (int j = 0; j < 2; j++)
                            {
                                if (crc[j] != Convert.ToByte( portNumber.ReadChar() ))
                                    ifDataCorrect = false;
                            }

                            if (ifDataCorrect)
                                error = true;
                            else
                            {
                                foreach (var item in tmpByteList)
                                {
                                    byteList.Add( item );
                                }

                                tmpByteList.Clear();
                                portNumber.WriteLine( ACK.ToString() );
                            }
                        }

                        if (error)
                        {
                            portNumber.DiscardInBuffer();
                            portNumber.WriteLine( NAK.ToString() );
                        }

                    } while (portNumber.ReadLine() == SOH.ToString());

                    portNumber.WriteLine( ACK.ToString() );

                    byte[] byteArray = byteList.ToArray();
                    File.WriteAllBytes( @"..\..\message.txt", byteArray );
                }
            }
            catch (TimeoutException te)
            {
                MessageBox.Show( te.Message );
            }
        }

        private static byte[] createCRC( byte[] package )
        {
            int crc = 0;
            foreach ( byte item in package )
            {
                crc = crc ^ item << 8;
                for ( int i = 0; i < 8; i++ )
                {
                    if ( Convert.ToBoolean( crc & 0x8000 ) )
                        crc = crc << 1 ^ 0x1021;
                    else
                        crc = crc << 1;
                }
            }

            crc = crc & 0xFFFF;

            return BitConverter.GetBytes( crc );
        }
    }
}