using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows;

namespace WpfApp4.Model
{
    /// <summary>
    /// klasa odwzorowujaca rzeczywisto model Xmodem-u
    /// </summary>
    public class Xmodem
    {
        #region Znaki sterujące

        //Start Of Heading - poczatek naglowka
        private const int SOH = 1;

        // End Of Text - koniec tekstu
        private const int EOT = 4;

        // AcKnowledge - potwierdzenie poprawnego odbioru pakietu
        private const int ACK = 6;

        // Negative AcKnowledge - sygnalizuje niepoprawne odebranie pakietu
        private const int NAK = 21;

        // Substitute - znak, dopelniajacy do 128bajtow
        private const int SUB = 26;

        #endregion

        // instancja klasy do obslugi portu szeregowego (COM)
        private readonly SerialPort portNumber;
        //public string Message { get; set; }

        /// <summary>
        /// konstruktor Xmodem
        /// </summary>
        /// <param name="portName">nazwa portu, konieczna aby zidentyfikowac port na którym ma dzialać xModem</param>
        /// <param name="baudrate"></param>
        public Xmodem( string portName, int baudrate )
        {
            // utworzenie instancji SerialPort obslugujacej port szeregowy
            // i inicjalizacja wartosci 
            portNumber = new SerialPort( portName )
            {
                // maksymalny czas dla odczytu z COM
                ReadTimeout = 5000,
                // predkosc BaudRate
                BaudRate = baudrate,
                // enkoding pozwalajacy na zachowanie wartosci wyzszych niz 127 - checksum
                //Encoding = System.Text.Encoding.GetEncoding( 28591 )
            };
            // otwarcie portu
            portNumber.Open();
        }

        /// <summary>
        /// Funkcja zamykajaca port szeregowy
        /// </summary>
        public void Close()
        {
            // wywoalnie metody klasy SerialPort Close() - zamkniecie portu
            portNumber.Close();
        }

        /// <summary>
        /// Funkcja wysylajaca tablice bajtów
        /// </summary>
        /// <param name="bytesToSend"> tablica bajtów do wysłania </param>
        public void Send( byte[] bytesToSend )
        {
            //otwarcie bloku try do obslugi wyjątków
            try
            {
                // sprawdzenie czy port jest otwarty
                if (!portNumber.IsOpen)
                    //jezeli nie to otwarcie portu
                    portNumber.Open();
                // zapewnia odpowiednie wywolanie IDispose zwalniajacego zasoby
                using (portNumber)
                {
                    // czekaj i sprawdzaj az odczyt z portu bedzie znakiem innym niz NAK
                    // jeżeli program będzie czekać na znak dłuzej niż ReadTimeout to nastąpi wyjątek
                    while (Convert.ToInt32( portNumber.ReadLine() ) != NAK) ;

                    // petla wykonujaca się liczbaBajtowDoPrzeslania / 128 aby zapewnic przesylanie 
                    // tylko 128 bajtowych pakietow
                    for (int i = 0; i <= bytesToSend.Length / 128; i++)
                    {
                        do
                        {
                            byte[] sentBytes = new byte[128];
                            // wpisz znak SOH do portu szeregowego
                            portNumber.WriteLine( SOH.ToString() );
                            // wpisz numer pakietu
                            portNumber.WriteLine( i.ToString() );
                            // wpisz 255 - numer pakietu - dopelnienie
                            portNumber.WriteLine( ( 255 - i ).ToString() );
                            // petla wysylajaca kolejne bajty z tablicy bajtow w pakietach po 128 bajtow
                            for (int j = 0; j < 128; j++)
                            {
                                // jezeli przeslano juz wszystkie bajty wiadomosci
                                if (i * 128 + j == bytesToSend.Length)
                                {
                                    //dopelnij pozostale bajty znakiem SUB
                                    for (int k = 0; k < 128 - j; k++)
                                    {
                                        //wpisz znak sub na port szeregowy
                                        portNumber.Write( Convert.ToChar( SUB ).ToString() );
                                        sentBytes[j + k] = SUB;
                                    }

                                    //przerwij petle 
                                    break;
                                }

                                //wpisz kolejny bajt wiadomosci do portu
                                portNumber.Write( Convert.ToChar( bytesToSend[i * 128 + j] ).ToString() );
                                sentBytes[j] = bytesToSend[i * 128 + j];
                            }

                            // utworz sume kontrolna i zapisz w tablicy bajtów crc
                            byte[] crc = createCheckSumCRC( sentBytes );

                            // przeslij sume kontrolna
                            for (int j = 0; j < 2; j++)
                            {
                                // wpisanie zawartosci tablicy crc do portu
                                portNumber.Write( Convert.ToChar( crc[j] ).ToString() );
                            }

                            // Jeżeli otrzymaleś NAK, powtórz, jeżeli nie idz do nastepnego pakietu
                        } while (Convert.ToInt32( portNumber.ReadLine() ) == NAK);
                    }

                    // po przeslaniu wszystkich pakietow wpisz EOT - koniec tekstu
                    do
                    {
                        // wysylaj koniec tekstu
                        portNumber.WriteLine( EOT.ToString() );
                        // dopoki nie uzyskasz odpowiedzi ACK
                    } while (Convert.ToInt32( portNumber.ReadLine() ) != ACK);
                }
            }
            // obsluga rzuconych wyjatkow
            catch (Exception ex)
            {
                // jezeli zgodnosc typu wyswietl okno dialogowe z wiadomoscia i zakoncz funkcje
                if (ex is InvalidOperationException || ex is TimeoutException || ex is IOException)
                {
                    MessageBox.Show( ex.Message );
                    return;
                }

                // jezeli inny wyjatek, rzuc go dalej
                throw;
            }
        }

        /// <summary>
        /// Funkcja slużąca do odbierania danych
        /// </summary>
        public void Receive()
        {
            try
            {
                // jezeli port zamkniety to sprobuj otworzyc
                if (!portNumber.IsOpen)
                    portNumber.Open();

                // lista bajtow tymczasowych/pomocniczych do oczytu
                List<byte> byteListHelper = new List<byte>();
                // lista odebranych bajtow po uzyskaniu pozytywnej sumy kontrolnej
                List<byte> recivedBytes = new List<byte>();

                // zapewnia odpowiednie wywolanie IDispose zwalniajacego zasoby
                using (portNumber)
                {
                    // flaga bledu ustawiana domyslnie na false
                    bool errorFlag = false;

                    // czekaj az na porcie pojawi sie instrukcja sterujaca SOH
                    do
                    {
                        // nadawaj NAK na port
                        portNumber.WriteLine( NAK.ToString() );
                        // dopóki nie otrzymasz sygnalu SOH lub nastapi Timeout
                    } while (Convert.ToInt32( portNumber.ReadLine() ) != SOH);

                    do
                    {
                        // sprawdz czy dopelnienie i numer pakietu sie zgadzaja
                        if (255 - Convert.ToInt32( portNumber.ReadLine() ) != Convert.ToInt32( portNumber.ReadLine() ))
                        {
                            // jezeli nie to ustaw flage błędu
                            errorFlag = true;
                        }
                        // w przeciwnym wypadku
                        else
                        {
                            // petla odbierajaca kolejne bajty
                            for (int i = 0; i < 128; i++)
                            {
                                // wczytywanie pojedynczego znaku z portu do zmiennej helper
                                int helper = portNumber.ReadChar();
                                // dodawanie wartosci do tymczasowej listy pomocniczej
                                byteListHelper.Add( Convert.ToByte( helper ) );
                            }


                            // flaga poprawności danych 
                            bool dataCorrectFlag = true;

                            // utworzenie sumy kontrolnej na podstawie odebranych danych
                            byte[] checkSumCRC = createCheckSumCRC( byteListHelper.ToArray() );
                            // petla porownujaca sumy kontrolne
                            // jezeli suma kontrolna rozna od odebranej


                            for (int j = 0; j < 2; j++)
                                if (checkSumCRC[j] != Convert.ToByte( portNumber.ReadChar() ))
                                {
                                    // ustaw flage poprawnosci danych na false
                                    dataCorrectFlag = false;
                                }


                            // jezeli dane poprawne
                            if (!dataCorrectFlag)
                                // ustawe flage bledu na true
                                errorFlag = true;
                            // jezeli nie
                            else
                            {
                                // dla kazdego odczytanego bajtu
                                foreach (var byteHelper in byteListHelper)
                                {
                                    // dodaj bajt do docelowej listy odebranych bajtow
                                    recivedBytes.Add( byteHelper );
                                }

                                // wyczysc liste pomocnicza 
                                byteListHelper.Clear();
                                // nadaj ACK
                                portNumber.WriteLine( ACK.ToString() );
                                Console.WriteLine( "ACK" );
                            }
                        }

                        // jezeli wystapil blad
                        if (errorFlag)
                        {
                            // porzuc zbufferowane w porcie dane
                            portNumber.DiscardInBuffer();
                            // nadaj sygnal sterujacy NAK
                            portNumber.WriteLine( NAK.ToString() );
                            Console.WriteLine( "NAK" );
                        }

                        // wykonuj instrukcje dopóki otrzymasz sygnal sterujacy SOH
                    } while (portNumber.ReadLine() == SOH.ToString());

                    // po odebraniu wszystkich bajtow nadaj ACK
                    portNumber.WriteLine( ACK.ToString() );

                    // zapisz odebrana wiadomosc w pliku txt
                    File.WriteAllBytes( @"..\..\message.txt",  recivedBytes.ToArray() );
                }
            }
            // obsluga wyjatkow
            catch (TimeoutException te)
            {
                //wyswietl wyjatek w oknie dialogowym
                MessageBox.Show( te.Message );
            }
        }

        // funkcja obliczajaca sumę kontrolna CRC-CCIT 16bit
        // wielomian to X(16) + X(12) + X(5) + 1, czyli hexadecymalnie 0x11021
        //http://www.drdobbs.com/implementing-the-ccitt-cyclical-redundan/199904926
        private static byte[] createCheckSumCRC( byte[] package )
        {
            // zmienna przechowywujaca obliczona sume kontrolna
            int checksumCRC = 0;
            // dla kazdego bajtu pakiecie
            foreach (byte _byte in package)
            {
                // operacja XOR pomiedzy poprzednim crc a kolejnym znakiem
                // zamieniona na zasadzie mlodsze bity ze starszymi 
                // odbicie lustrzane
                checksumCRC = checksumCRC ^ _byte << 8;
                // dla kazdego bitu w bajcie
                for (int i = 0; i < 8; i++)
                {
                    // jezeli  checksum ma 1 na najstarszym bicie
                    if (Convert.ToBoolean( checksumCRC & 0x8000 ))
                        // dzielenie przez 2 i XOR z wielomianem
                        checksumCRC = (checksumCRC >> 1) ^ 0x1021;
                    else
                        // dzielenie przez 2
                        checksumCRC = checksumCRC >> 1;
                }
            }

            // zwroc sume kontrolna
            return BitConverter.GetBytes( checksumCRC );
        }
    }
}