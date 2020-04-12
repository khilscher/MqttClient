using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Windows.UI.Core;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using MQTT.Models;

namespace MQTT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private MqttClient client;
        private string broker;
        private int port;
        private bool secure;
        private MqttSslProtocols sslprotocol;
        private MqttProtocolVersion protocolversion;
        private string username;
        private string password;
        private string clientId;
        private string topic;
        private bool publish = false;
        private double temp;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {

            var broker = txtBoxMqttBroker.Text.Trim();
            if (broker.Length == 0)
            {
                // Missing broker
                NotifyUser("Enter a hostname for the MQTT broker.");
            }
            else
            {
                int port = -1;
                if (!(int.TryParse(txtBoxMqttPort.Text.Trim(), out port)))
                {
                    // Invalid port
                    NotifyUser("Enter a valid port number");
                }
                else
                {

                    if (txtBoxTopic.Text.Trim().Length == 0)
                    {
                        // Invalid topic
                        NotifyUser("Enter a valid topic");
                    }
                    else
                    {
                        // Parse the input values
                        var sslprotocol = (MqttSslProtocols)Enum.Parse(typeof(MqttSslProtocols), "None");
                        var protocolversion = (MqttProtocolVersion)Enum.Parse(typeof(MqttProtocolVersion), "Version_3_1_1");
                        
                        // Connect to Mqtt Broker
                        try
                        {
                            this.broker = txtBoxMqttBroker.Text;
                            this.clientId = txtBoxDeviceID.Text;
                            this.port = port;
                            this.secure = false;
                            this.sslprotocol = sslprotocol;
                            this.protocolversion = protocolversion;
                            this.username = txtBoxUsername.Text;
                            this.password = txtBoxPassword.Text;

                            client = new MqttClient(this.broker, this.port, this.secure, this.sslprotocol);
                            client.ProtocolVersion = this.protocolversion;

                            // Callback for message subscription
                            client.MqttMsgPublishReceived += _client_MqttMsgPublishReceived;

                            // MQTT return codes https://www.hivemq.com/blog/mqtt-essentials-part-3-client-broker-connection-establishment/
                            // https://www.eclipse.org/paho/clients/dotnet/api/html/4158a883-de72-1ec4-2209-632a86aebd74.htm
                            byte resp = client.Connect(this.clientId, this.username, this.password);
                            NotifyUser("Connect() Response: " + resp.ToString());
                        }
                        catch (Exception ex)
                        {
                            NotifyUser(ex.Message + ": " + ex.InnerException);
                        }
                    }
                }
            }
        }

        // Display message in status textbox
        public void NotifyUser(string strMessage)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => UpdateStatus(strMessage));
            }
        }

        // Display a message in the status box
        private void UpdateStatus(string strMessage)
        {
            txtBoxStatus.Text += strMessage + "\n";
        }

        // Hack to allow status textbox to autoscroll to bottom
        private void txtBoxStatus_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(txtBoxStatus, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }
        
        // Clear the status textbox
        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            txtBoxStatus.Text = "";
        }

        // Start publishing some random Mqtt messages
        private void btnPublish_Click(object sender, RoutedEventArgs e)
        {
            
            var task = Task.Run(() => Publish());

        }

        // Generate some random data and publish
        private void Publish()
        {
            publish = true;

            if (client != null && client.IsConnected)
            {
                while (publish)
                {
                    //Random rnd = new Random();

                    var msgObj = new
                    {
                        //temperature = rnd.Next(20, 80),
                        temperature = temp
                    };

                    string message = JsonConvert.SerializeObject(msgObj);
                    ushort resp = client.Publish(this.topic, System.Text.Encoding.UTF8.GetBytes(message), 0, true);
                    NotifyUser("Publish() Response: " + resp.ToString());

                    Thread.Sleep(200);
                }
            }
            else
            {
                NotifyUser("Not connected");
            }
        }

        // Callback when message is received in topic subscription
        private void _client_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {

            NotifyUser($"Msg rec'd on topic {e.Topic.ToString()}: {System.Text.UTF8Encoding.UTF8.GetString(e.Message)}");

            Telemetry data = JsonConvert.DeserializeObject<Telemetry>(System.Text.UTF8Encoding.UTF8.GetString(e.Message));

            // Update radial guage on UI thread with MQTT response value
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateRadial(data.temperature));

        }

        // Update radial gauge with value from MQTT payload
        private void UpdateRadial(double temperature)
        {
            RadialGaugeControl.Value = temperature;
        }

        // Subscribe to a topic
        private void btnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.IsConnected)
            {
                this.topic = txtBoxTopic.Text;

                ushort resp = client.Subscribe(
                    new string[] { this.topic },
                    new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

                NotifyUser("Subscribe() Response: " + resp.ToString());

            }
            else
            {
                NotifyUser("Not connected");
            }
        }

        // Disconnect from Mqtt broker
        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.IsConnected)
            {
                client.Disconnect();
                NotifyUser("Disconnect()");
            }
            else
            {
                NotifyUser("Not connected");
            }
        }

        // Unsubscribe from Mqtt topic
        private void btnUnsubscribe_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.IsConnected)
            {
                ushort resp = client.Unsubscribe(
                    new string[] { this.topic });

                NotifyUser("Unsubscribe() Response: " + resp.ToString());
            }
            else
            {
                NotifyUser("Not connected");
            }
        }

        // Stop publishing messages to Mqtt topic
        private void btnStopPublish_Click(object sender, RoutedEventArgs e)
        {
            publish = false;
        }

        // Update temp based on slider value
        private void sliderTemp_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            temp = e.NewValue;
        }
    }
}
