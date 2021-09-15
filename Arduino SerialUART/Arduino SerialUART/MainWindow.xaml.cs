using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arduino_SerialUART
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {


        //New approach using windows.devices.serialcommunicartion namespace
        private SerialDevice serialPort;
        DataWriter dataWriteObject;
        DataReader dataReaderObject;

        private readonly ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        public MainWindow()
        {
            this.InitializeComponent();
            splitView.PaneOpened += OnPaneOpened;
            BtnConnect.IsEnabled = false;
            BtnSend.IsEnabled = false;
            listOfDevices = new ObservableCollection<DeviceInformation>();
            UpdateDevices();
        }

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        private async void ListAvailablePorts()
        {
            // cmbPortName.SelectedIndex = -1;
            cmbPortName.Items.Clear();

            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);


                status.Text = "Select a device and connect";

                //for (int i = 0; i < dis.Count; i++)
                // {
                // listOfDevices.Add(dis[i]);
                //}
                foreach (var device in dis)
                {

                    cmbPortName.Items.Add(device);

                }

                // DeviceListSource.Source = listOfDevices;
                BtnConnect.IsEnabled = true;
              
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        /// <summary>
        /// OnConnect: Action to take when 'Connect' button is clicked
        /// - Get the selected device index and use Id to create the SerialDevice object
        /// - Configure default settings for the serial port
        /// - Create the ReadCancellationTokenSource token
        /// - Start listening on the serial port input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnConnect()
        {
            object selection = cmbPortName.SelectedItem;

            if (selection == null)
            {
                status.Text = "Select a device and connect";

                teachingTip.Title = "No Port Selected";
                teachingTip.Subtitle = "Select and Setup Port before Clicking Connect";
                teachingTip.Target = BtnConnect;
                teachingTip.IsOpen = true;

                return;
            }

            if (cmbPortName.SelectedIndex != -1 & cmbPortBaudRate.SelectedIndex != -1 & cmbPortParity.SelectedIndex != -1 & cmbPortDataBits.SelectedIndex != -1 & cmbPortStopBits.SelectedIndex != -1)
            {
                DeviceInformation entry = (DeviceInformation)selection;

                try
                {
                    serialPort = await SerialDevice.FromIdAsync(entry.Id);
                    if (serialPort == null)
                    {
                        return;
                    }

                    ComboBoxItem cbi_baud = (ComboBoxItem)cmbPortBaudRate.SelectedItem;
                    ComboBoxItem cbi_parity = (ComboBoxItem)cmbPortParity.SelectedItem;
                    ComboBoxItem cbi_data = (ComboBoxItem)cmbPortDataBits.SelectedItem;
                    ComboBoxItem cbi_stop = (ComboBoxItem)cmbPortStopBits.SelectedItem;

                    // Configure serial settings
                    serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                    serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                    serialPort.BaudRate = uint.Parse(cbi_baud.Content.ToString()); //convert Text to Integer
                    serialPort.Parity = (SerialParity)Enum.Parse(typeof(SerialParity), cbi_parity.Content.ToString()); //convert Text to Parity
                    serialPort.StopBits = (SerialStopBitCount)Enum.Parse(typeof(SerialStopBitCount), cbi_stop.Content.ToString()); //convert Text to stop bits
                    serialPort.DataBits = ushort.Parse(cbi_data.Content.ToString()); //convert Text to Integer
                    serialPort.Handshake = SerialHandshake.None;

                    // Change 'Connect' button to "Disconnect"
                    BtnConnect.Content = "Disconnect";
                    BtnRefresh.IsEnabled = false;
                    DisableSettings();


                    //enable led panel
                    TxDataGrid.Visibility = Visibility.Visible;

                    // Display configured settings
                    status.Text = "Serial port configured successfully: ";
                    status.Text += serialPort.BaudRate + "-";
                    status.Text += serialPort.DataBits + "-";
                    status.Text += serialPort.Parity.ToString() + "-";
                    status.Text += serialPort.StopBits;

                    // Set the RcvdText field to invoke the TextChanged callback
                    // The callback launches an async Read task to wait for data
                    rcvdText.Text = "Waiting for data...";

                    // Create cancellation token object to close I/O operations when closing the device
                    ReadCancellationTokenSource = new CancellationTokenSource();

                    // Enable 'WRITE' button to allow sending data
                    BtnSend.IsEnabled = true;

                    Listen();
                }
                catch (Exception ex)
                {
                    status.Text = ex.Message;
                    BtnConnect.IsEnabled = true;
                    BtnSend.IsEnabled = false;

                    contentDialog.Title = "COM Port unavailable";
                    TxbNoticeDialog.Text = "Could not open the COM port. Most likely it is already in use, has been removed, or is unavailable. Check Status";
                    contentDialog.XamlRoot = MainGrid   .XamlRoot;
                    _ = await contentDialog.ShowAsync();
                }
            }
            else
            {
                contentDialog.Title = "Serial Port Interface";
                TxbNoticeDialog.Text = "Please select all the COM Serial Port Settings";
                contentDialog.XamlRoot = MainGrid.XamlRoot;
                _ = await contentDialog.ShowAsync();
            }
        }

        /// <summary>
        /// BtnSend_Click: Action to take when 'WRITE' button is clicked
        /// - Create a DataWriter object with the OutputStream of the SerialDevice
        /// - Create an async task that performs the write operation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync(TxtSend.Text);
                }
                else
                {
                    status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                status.Text = "BtnSend_Click: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        /// <summary>
        /// TxtSend: Action when enter key is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TxtSend_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                BtnSend_Click(sender, e);
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync(string message)
        {
            Task<UInt32> storeAsyncTask;

            if (message.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(message);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = message + ", ";
                    status.Text += "bytes written successfully!";
                }
                TxtSend.Text = "";
            }
            else
            {
                status.Text = "Enter the text you want to write and then click on 'Send'";
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (TaskCanceledException tce)
            {
                status.Text = tce.Message;

                status.Text = "Reading task was cancelled, closing device and cleaning up";
                CloseDevice();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            using CancellationTokenSource childCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(childCancellationTokenSource.Token);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                rcvdText.Text = "";
                string data = dataReaderObject.ReadString(bytesRead);
                Paragraph paragraph = new();
                Run run = new();

                run.Text = data;
                paragraph.Inlines.Add(run);
                rtxtDataArea.Blocks.Add(paragraph);

                ChangeLed(data);

                status.Text = "bytes read successfully!";
            }
        }

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;

            BtnConnect.IsEnabled = true;
            BtnSend.IsEnabled = false;
            rcvdText.Text = "";
            listOfDevices.Clear();
        }

        /// <summary>
        /// OnDisconnect: Action to take when 'Disconnect and Refresh List' is clicked on
        /// - Cancel all read operations
        /// - Close and dispose the SerialDevice object
        /// - Enumerate connected devices
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDisconnect()
        {
            try
            {
                status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();

                BtnConnect.Content = "Connect";
                BtnSend.IsEnabled = false;
                BtnRefresh.IsEnabled = true;
                EnableSettings();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        /// <summary>
        /// ConnectMe: Action of Connect/Disconnect Button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectMe(object sender, RoutedEventArgs e)
        {
            if (BtnConnect.Content.ToString() == "Disconnect")
            {
                OnDisconnect();
            }
            else
            {
                OnConnect();
            }
        }


        /// <summary>
        /// Refresh button action
        /// </summary>
        private void UpdateDevices()
        {
            cmbPortName.Items.Clear();
            ListAvailablePorts();
        }

  


        private void OnPaneOpened(SplitView sender, object args)
        {
            string currentTheme = sender.RequestedTheme == ElementTheme.Default ? App.Current.RequestedTheme.ToString() : sender.RequestedTheme.ToString();
            themePanel.Children.Cast<RadioButton>().FirstOrDefault(c => c?.Tag?.ToString() == currentTheme).IsChecked = true;

        }
        private void OnAbout(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = false;
        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = !splitView.IsPaneOpen;
        }

        private void OnThemeRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            string selectedTheme = ((RadioButton)sender)?.Tag?.ToString();
            if (selectedTheme != null)
            {
                ((sender as RadioButton).XamlRoot.Content as SplitView).RequestedTheme = GetEnum<ElementTheme>(selectedTheme);
            }
        }

        private void OnHelp(object sender, RoutedEventArgs e)
        {
            teachingTip.Target = TxtSend;
            teachingTip.Title = "Serial Port Board";
            teachingTip.Subtitle = "Select and set Serial Port, Connect, type message and presss end";
            teachingTip.IsOpen = true;
        }

        private static TEnum GetEnum<TEnum>(string text) where TEnum : struct
        {
            if (!typeof(TEnum).GetTypeInfo().IsEnum)
            {
                throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
            }
            return (TEnum)Enum.Parse(typeof(TEnum), text);
        }

        private void RefreshPorts(object sender, RoutedEventArgs e)
        {
            UpdateDevices();
        }

        private void DisableSettings()
        {
            cmbPortName.IsEnabled = false;
            cmbPortBaudRate.IsEnabled = false;
            cmbPortParity.IsEnabled = false;
            cmbPortDataBits.IsEnabled = false;
            cmbPortStopBits.IsEnabled = false;
            BtnRefresh.IsEnabled = false;
            TxDataGrid.Visibility = Visibility.Visible;
            powerSystemGrid.Visibility = Visibility.Visible;
            portSettingGrid.Visibility = Visibility.Collapsed;
        }
        private void EnableSettings()
        {
            cmbPortName.IsEnabled = true;
            cmbPortBaudRate.IsEnabled = true;
            cmbPortParity.IsEnabled = true;
            cmbPortDataBits.IsEnabled = true;
            cmbPortStopBits.IsEnabled = true;
            BtnRefresh.IsEnabled = true;
            TxDataGrid.Visibility = Visibility.Collapsed;
            portSettingGrid.Visibility = Visibility.Visible;
            powerSystemGrid.Visibility = Visibility.Collapsed;
        }


/// <summary>
/// ChangeLed: Response to data recieved to change status of Led
/// </summary>
/// <param name="data"></param>


        private void ChangeLed(string data)
        {
            if (data != null) //if text mode is selected, send data as tex

            //if (data.Length > 2)
            //    {
            //        _ = data.Split('\n');
            //    }


            {
                _ = data.Split();

                switch (data[0])
                {
                    case '1':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '2':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '3':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '4':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '5':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '6':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '7':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '8':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;
                    case '9':
                        {
                            if (data[1] == '1')
                            { LedOn(data); }
                            else
                            { LedOff(data); }
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// LEDON: routine for switcing LED status ellipse OM
        /// </summary>
        /// <param name="data"></param>
        private void LedOn(string data)
        {
            _ = data.Split();
            string LedName = "ElpLed" + data[0];

            Ellipse el = TxDataGrid.FindName(LedName) as Ellipse;

            el.Fill = new SolidColorBrush(Microsoft.UI.Colors.Green);
        }

        /// <summary>
        /// LEDOFF: routine for switcing LED status ellipse OFF
        /// </summary>
        /// <param name="data"></param>
        private void LedOff(string data)
        {
            _ = data.Split();
            string LedName = "ElpLed" + data[0];

            Ellipse el = TxDataGrid.FindName(LedName) as Ellipse;

            el.Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        /// <summary>
        /// SwitchLedAsync: Task to respond to toggle switch toggle, and send preconfigured ON or OFF
        /// Message to Arduino
        /// </summary>
        /// <param name="ledId"></param>
        /// <returns></returns>

        private async Task SwitchLedAsync(string ledId)
        {

            if (ledId != null)
            {
                string TgSwitchName = "TgsLed" + ledId;

                ToggleSwitch ts = TxDataGrid.FindName(TgSwitchName) as ToggleSwitch;

                try
                {
                    if (serialPort != null)
                    {
                        // Create the DataWriter object and attach to OutputStream
                        dataWriteObject = new DataWriter(serialPort.OutputStream);

                        //determine state of the toggleswitch
                        // generate appropriate message using ledID e.g if ledId = "1", ON = L11 and Off = L10
                        // Launch the WriteAsync task to perform the write
                        //
                        if (ts.IsOn)
                        {
                            string message = "L" + ledId + "1";
                            await WriteAsync(message);
                        }
                        else
                        {
                            string message = "L" + ledId + "0";
                            await WriteAsync(message);
                        }
                    }
                    else
                    {
                        status.Text = "Select a LED Switch";
                    }
                }
                catch (Exception ex)
                {
                    status.Text = TgSwitchName + ": " + ex.Message;
                }
                finally
                {
                    // Cleanup once complete
                    if (dataWriteObject != null)
                    {
                        dataWriteObject.DetachStream();
                        dataWriteObject = null;
                    }
                }
            }
        }



        private void TgsLed1_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("1");
        }

        private void TgsLed2_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("2");
        }

        private void TgsLed3_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("3");
        }

        private void TgsLed4_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("4");
        }

        private void TgsLed5_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("5");
        }

        private void TgsLed6_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("6");
        }

        private void TgsLed7_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("7");
        }

        private void TgsLed8_Toggled(object sender, RoutedEventArgs e)
        {
            _ = SwitchLedAsync("8");
        }


        /// <summary>
        /// TgbAll_ClickAsync:  Action to switch all the ligth ON or OFF.
        /// 
        /// to use add  Click="TgbAll_ClickAsync" to TgbAll
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TgbAll_ClickAsync(object sender, RoutedEventArgs e)
        {

            try
            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    if (TgbAll.Content.ToString() == "All ON")
                    {
                        await WriteAsync("X1");
                        TgbAll.Content = "All OFF";
                        //    ResetLedSwitches();
                    }
                    else
                    {
                        await WriteAsync("X0");
                        TgbAll.Content = "All ON";
                        //     ResetLedSwitches();
                    }
                }
                else
                {
                    status.Text = "Select a LED Switch";
                }
            }
            catch (Exception ex)
            {
                status.Text = TgbAll + ": " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }

            //ResetLedSwitches();
        }

        private void ResetLedSwitches()
        {
            if (TgbAll.Content.ToString() == "All OFF")
            {
                TgsLed1.IsOn  = false;
                TgsLed2.IsOn  = false;
                TgsLed3.IsOn  = false;
                TgsLed4.IsOn  = false;
                TgsLed5.IsOn  = false;
                TgsLed6.IsOn  = false;
                TgsLed7.IsOn  = false;
                TgsLed8.IsOn = false; 
                            }
            else if (TgbAll.Content.ToString() == "All ON")
            {
                TgsLed1.IsOn = true;
                TgsLed2.IsOn = true;
                TgsLed3.IsOn = true;
                TgsLed4.IsOn = true;
                TgsLed5.IsOn = true;
                TgsLed6.IsOn = true;
                TgsLed7.IsOn = true;
                TgsLed8.IsOn = true;
            }
        }
    }
}
