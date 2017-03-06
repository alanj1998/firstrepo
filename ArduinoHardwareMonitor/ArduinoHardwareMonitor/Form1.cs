using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using OpenHardwareMonitor.Hardware;
using Temboo.Core;
using Temboo.Library.Google.Gmail;

namespace ArduinoHardwareMonitor
{
    public partial class Form1 : Form
    {
        /*
         *  Class variables used in all methods.
         */
         //Getting access to the users computer by turning it into an object
        Computer userPc = new Computer()
        {
            CPUEnabled = true,
            RAMEnabled = true
        };

        //Necessary vars used throughout the progamme
        float cpuTemp, ramClock;
        private SerialPort arduinoPort = new SerialPort();
        string ram;
        DateTime oldTime = DateTime.UtcNow;
        DateTime currentTime;
        int messagesSent = 0;

        /*
         *  These methods are used to initialise the programme.
         *  When the programme starts, all the elements are placed onto a windows form.
         *  Then all the active parts are initialised and added to a combo box.
         *  Then the intervals are added to the interval box.
         */
        public Form1()
        {
            InitializeComponent();
            SetInterval();
            PortInit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            userPc.Open();
        }

        private void PortInit()
        {
            try
            {
                arduinoPort.Parity = Parity.None;
                arduinoPort.DataBits = 8;
                arduinoPort.StopBits = StopBits.One;
                arduinoPort.Handshake = Handshake.None;
                arduinoPort.RtsEnable = true;
                arduinoPort.DtrEnable = true;

                portBox.Items.Clear();
                string[] pcPorts = SerialPort.GetPortNames();
                foreach (var ports in pcPorts)
                {
                    portBox.Items.Add(ports);
                }

                arduinoPort.BaudRate = 9600;
            }
            catch(Exception portInitError)
            {
                MessageBox.Show(portInitError.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetInterval()
        {
            for (int i = 100; i <= 1000; i += 100)
            {
                intrvlBox.Items.Add(i);
            }
        }

        /*
         *  The buttons here are all given a seperate method.
         *  When the connecect button is clicked, the port is opened and the timer is enabled to begin updates.
         *  When the disconnect button is clicked, then a DIS message is sent through the serial con.
         *  The email check button checks for the @ symbol in the email.
         */
        private void conBtn_Click(object sender, EventArgs e)
        {
            if (portBox.Text == "" || intrvlBox.Text == "" || !emailBox.Text.Contains("@"))
            {
                MessageBox.Show("Make sure no fields are empty and try again!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                try
                {
                    if (!arduinoPort.IsOpen)
                    {
                        arduinoPort.PortName = portBox.Text;
                        arduinoPort.Open();
                        timer1.Enabled = true;
                        timer1.Interval = Convert.ToInt32(intrvlBox.Text);
                        status.Text = "Connected";
                        toolStatusLbl.Text = "Sending Data...";
                    }
                }
                catch (Exception portOpenErr)
                {
                    MessageBox.Show(portOpenErr.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void disBtn_Click(object sender, EventArgs e)
        {
            try
            {
                arduinoPort.Write("DIS*");
                arduinoPort.Close();
            }
            catch (Exception portCloseErr)
            {
                MessageBox.Show(portCloseErr.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            status.Text = "Disconnected";
            timer1.Enabled = false;
            toolStatusLbl.Text = "Connect to Arduino...";
        }

        private void emailCheckBtn_Click(object sender, EventArgs e)
        {
            if (emailBox.Text.Contains('@'))
            {
                MessageBox.Show("Email is Good!", "Email", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (emailBox.Text == "")
            {
                MessageBox.Show("Please enter a proper email address!", "Email Error!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show("Please enter a proper email address!", "Email Error!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        //When the email box is clicked, the text dissapears
        private void emailBox_MouseClick(object sender, MouseEventArgs e)
        {
            emailBox.Text = "";
        }

        /*
         *  Once the connect button is clicked, the timer1_tick method is deployed which works as a trigger for constamt upddates.
         *  When it is time to update, the GetStatus method is deployed that gets the current temps and ram speed.
         *  It is then sent to be printed onto the arduino screen.
         */
        private void timer1_Tick(object sender, EventArgs e)
        {
            GetStatus();
        }

        private void GetStatus()
        {
            foreach (var hardware in userPc.Hardware)
            {
                if (hardware.HardwareType == HardwareType.CPU)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            cpuTemp = sensor.Value.GetValueOrDefault();
                        }
                    }
                }
                if (hardware.HardwareType == HardwareType.RAM)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load)
                        {
                            ramClock = sensor.Value.GetValueOrDefault();
                            ram = String.Format("{0:00.0}", ramClock);
                        }
                    }
                }
                arduinoPort.Write(cpuTemp + "*" + ram + "@");
                currentTime = DateTime.UtcNow;
                if (cpuTemp > 60 && (messagesSent == 0 || currentTime.Subtract(oldTime) > TimeSpan.FromMinutes(3)))
                    SendEmail(emailBox.Text);
            }
        }

        /*
         *  If the temperature is above 60 degrees an email is sent using the Temboo and Gmail service.
         */
        private void SendEmail(string email)
        { 
            TembooSession newSession = new TembooSession("alanj1998", "GmailSender", "lTcbyjr8RYAf7MhPKnTWzspL3Lucpu2C");
            SendEmail sendEmailTemboo = new SendEmail(newSession);

            sendEmailTemboo.setFromAddress("\"Hardware Monitors\" <alan.j2304@gmail.com>");
            sendEmailTemboo.setUsername("alan.j2304@gmail.com");
            sendEmailTemboo.setToAddress(email);
            sendEmailTemboo.setSubject("CPU Overheat!");
            sendEmailTemboo.setMessageBody("Dear User, \nYour CPU is overheating! Quickly check your computer!\n\nYours Faithfully,\nHardware Monitor Team");
            sendEmailTemboo.setPassword("qvzyhmjqzwmtdzuf");

            SendEmailResultSet results = sendEmailTemboo.execute();
            messagesSent++;
            oldTime = DateTime.UtcNow;
        }
    }
}