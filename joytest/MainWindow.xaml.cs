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

// Don't forget to add this
using vJoyInterfaceWrap;

// including the M2Mqtt Library
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Threading;
using Newtonsoft.Json;

namespace joytest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Declaring one joystick (Device id 1) and a position structure. 
        static public vJoy joystick;
        static public vJoy.JoystickState iReport;
        static public uint id = 1;

        MqttClient client;
        string clientId;
        private long maxval;
        private int nButtons;

        public int ContPovNumber { get; private set; }
        public int DiscPovNumber { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            string BrokerAddress = "192.168.2.1";

            client = new MqttClient(BrokerAddress);

            // register a callback-function (we have to implement, see below) which is called by the library when a message was received
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

            // use a unique id as client id, each time we start the application
            clientId = Guid.NewGuid().ToString();

            client.Connect(clientId);

            // whole topic
            string Topic = "/vjoy/#";

            setupVjoy();

            // subscribe to the topic with QoS 2
            client.Subscribe(new string[] { Topic }, new byte[] { 2 });   // need arrays as parameters because we can                                                                           // subscribe to different topics with one call
        }

        public void writeMessage(String s)
        {
            Dispatcher.Invoke(callback: delegate {  // we need this construction because the receiving code in the library and the UI with textbox run on different threads
                myTextBox.AppendText(textData: "\n" + s);
                if (myTextBox.LineCount > 64)
                {
                    myTextBox.Text.Remove(0, myTextBox.GetLineLength(0));
                }
                myTextBox.ScrollToEnd();
            });
        }

        // this code runs when a message was received
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string ReceivedMessage = Encoding.UTF8.GetString(e.Message);
            writeMessage(e.Topic);
            writeMessage(ReceivedMessage);

            dynamic stuff = JsonConvert.DeserializeObject(ReceivedMessage);
            if( ! (stuff is long) && joystick != null)
            {

                String type = stuff["type"];
                switch (type.ToLower())
                {
                    case "b":
                        UInt32 bi = stuff["index"];
                        if( bi <= nButtons)
                        {
                            joystick.SetBtn(true, id, bi);
                        }
                        break;
                    case "j":
                        HID_USAGES hv;
                        String index = stuff["index"];
                        Int32 value = stuff["value"];
                        if( value > maxval )
                        {
                            //value = Convert.ToInt32(maxval);
                        }
                        if (Enum.TryParse(index, out hv))
                        {
                            if (Enum.IsDefined(typeof(HID_USAGES), hv))
                            {
                                joystick.SetAxis(value, id, hv);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }            
        }

        /**
         * Code is just reference from vJoy.
         */
        public void setupVjoy()
        {
            // Create one joystick object and a position structure.
            joystick = new vJoy();
            iReport = new vJoy.JoystickState();


            // Device ID can only be in the range 1-16
            /*
            if (args.Length > 0 && !String.IsNullOrEmpty(args[0]))
                id = Convert.ToUInt32(args[0]);
            if (id <= 0 || id > 16)
            {
                Console.WriteLine("Illegal device ID {0}\nExit!", id);
                return;
            }
            */

            // Get the driver attributes (Vendor ID, Product ID, Version Number)
            if (!joystick.vJoyEnabled())
            {
                writeMessage(String.Format("vJoy driver not enabled: Failed Getting vJoy attributes."));
                return;
            }
            else
            {
                writeMessage(String.Format("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n", joystick.GetvJoyManufacturerString(), joystick.GetvJoyProductString(), joystick.GetvJoySerialNumberString()));
            }

            // Get the state of the requested device
            VjdStat status = joystick.GetVJDStatus(id);
            switch (status)
            {
                case VjdStat.VJD_STAT_OWN:
                    writeMessage(String.Format("vJoy Device {0} is already owned by this feeder\n", id));
                    break;
                case VjdStat.VJD_STAT_FREE:
                    writeMessage(String.Format("vJoy Device {0} is free\n", id));
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    writeMessage(String.Format("vJoy Device {0} is already owned by another feeder\nCannot continue", id));
                    return;
                case VjdStat.VJD_STAT_MISS:
                    writeMessage(String.Format("vJoy Device {0} is not installed or disabled\nCannot continue", id));
                    return;
                default:
                    writeMessage(String.Format("vJoy Device {0} general error\nCannot continue", id));
                    return;
            };
            

            // Check which axes are supported
            bool AxisX = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_X);
            bool AxisY = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Y);
            bool AxisZ = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Z);
            bool AxisRX = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_RX);
            bool AxisRZ = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_RZ);
            // Get the number of buttons and POV Hat switchessupported by this vJoy device
            nButtons = joystick.GetVJDButtonNumber(id);
            ContPovNumber = joystick.GetVJDContPovNumber(id);
            DiscPovNumber = joystick.GetVJDDiscPovNumber(id);

            // Print results
            /*
            writeMessage(String.Format("vJoy Device {0} capabilities:", id));
            writeMessage(String.Format("Numner of buttons\t\t{0}", nButtons));
            writeMessage(String.Format("Numner of Continuous POVs\t{0}", ContPovNumber));
            writeMessage(String.Format("Numner of Descrete POVs\t\t{0}", DiscPovNumber));
            writeMessage(String.Format("Axis X\t\t{0}", AxisX ? "Yes" : "No"));
            writeMessage(String.Format("Axis Y\t\t{0}", AxisX ? "Yes" : "No"));
            writeMessage(String.Format("Axis Z\t\t{0}", AxisX ? "Yes" : "No"));
            writeMessage(String.Format("Axis Rx\t\t{0}", AxisRX ? "Yes" : "No"));
            writeMessage(String.Format("Axis Rz\t\t{0}", AxisRZ ? "Yes" : "No"));
            */
            // Test if DLL matches the driver
            UInt32 DllVer = 0, DrvVer = 0;
            bool match = joystick.DriverMatch(ref DllVer, ref DrvVer);
            if (match)
                Console.WriteLine("Version of Driver Matches DLL Version ({0:X})\n", DllVer);
            else
                Console.WriteLine("Version of Driver ({0:X}) does NOT match DLL Version ({1:X})\n", DrvVer, DllVer);


            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && (!joystick.AcquireVJD(id))))
            {
                Console.WriteLine("Failed to acquire vJoy device number {0}.\n", id);
                return;
            }
            else
                Console.WriteLine("Acquired: vJoy device number {0}.\n", id);


            maxval = 0;

            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_X, ref maxval);

            joystick.ResetAll();

        }
    }
}
