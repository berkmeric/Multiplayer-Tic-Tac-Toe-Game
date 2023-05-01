using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public partial class Form1 : Form
    {

        bool terminating = false;
        bool connected = false;
        Button[,] gameBoardButtons = new Button[3, 3];
        Socket clientSocket;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
            InitializeGameBoard();
        }

        private void InitializeGameBoard()
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    gameBoardButtons[i, j] = new Button();
                    gameBoardButtons[i, j].Size = new Size(100, 100);
                    gameBoardButtons[i, j].Location = new Point(100 * i, 100 * j);
                    gameBoardButtons[i, j].Click += new EventHandler(GameBoardButton_Click);
                    gameBoardButtons[i, j].Enabled = false;
                    this.Controls.Add(gameBoardButtons[i, j]);
                }
            }
        }



        private void button_connect_Click(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = textBox_ip.Text;

            int portNum;
            if(Int32.TryParse(textBox_port.Text, out portNum))
            {
                try
                {
                    clientSocket.Connect(IP, portNum);
                    button_connect.Enabled = false;
                    textBox_message.Enabled = true;
                    button_send.Enabled = true;
                    connected = true;
                    logs.AppendText("Connected to the server!\n");


                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            gameBoardButtons[i, j].Enabled = true;
                        }
                    }


                    Thread receiveThread = new Thread(Receive);
                    receiveThread.Start();

                }
                catch
                {
                    logs.AppendText("Could not connect to the server!\n");
                }
            }
            else
            {
                logs.AppendText("Check the port\n");
            }

        }

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    Byte[] buffer = new Byte[64];
                    clientSocket.Receive(buffer);

                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                    if (incomingMessage == "invalid move")
                    {


                        logs.AppendText("Invalid move: The selected square has already been selected.\n");
                    }
                    else
                    {

                        

                        logs.AppendText("Server: " + incomingMessage + "\n");
                    }
                }
                catch
                {
                    if (!terminating)
                    {
                        logs.AppendText("The server has disconnected\n");
                        button_connect.Enabled = true;
                        for (int i = 0; i < 3; i++)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                gameBoardButtons[i, j].Enabled = false;
                            }
                        }
                    }

                    clientSocket.Close();
                    connected = false;
                }
            }
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }

        private void button_send_Click(object sender, EventArgs e)
        {
            string message = textBox_message.Text;

            if(message != "" && message.Length <= 64)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
            }

        }

        private void GameBoardButton_Click(object sender, EventArgs e)
        {
            Button clickedButton = sender as Button;
            int x = clickedButton.Location.X / 100;
            int y = clickedButton.Location.Y / 100;

            logs.AppendText("Client: " + x + ", " + y + "\n");
            

            if (connected)
            {
                string message = $"move {x} {y}";
                Byte[] buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
            }
        }

        private void textBox_port_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void logs_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
