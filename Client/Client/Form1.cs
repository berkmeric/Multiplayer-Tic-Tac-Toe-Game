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
                    gameBoardButtons[i, j].Font = new Font("Arial", 24, FontStyle.Bold);
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
                    button_send.Enabled = false;
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

                    if (incomingMessage == "play" || incomingMessage == "wait")
                    {
                        if (incomingMessage == "play")
                        {
                            logs.AppendText("Server: It is your turn to play. Please select a move..." + "\n");
                            button_send.Enabled = true;
                        }
                        else
                        {
                            logs.AppendText("Server: Your opponent is making a move. Please wait your turn..." + "\n");
                            button_send.Enabled = false;
                        }
                    }
                    else if (incomingMessage == "draw" || incomingMessage == "win" || incomingMessage == "loose")
                    {
                        button_send.Enabled = false;
                        button_playagain.Enabled = true;
                        if (incomingMessage == "draw")
                        {
                            logs.AppendText("Server: The game ended in a draw.\n");
                        }
                        else if (incomingMessage == "win")
                        {
                            logs.AppendText("Server: Congratulations you won the game.\n");
                        }
                        else if (incomingMessage == "loose")
                        {
                            logs.AppendText("Server: You lost. Better luck next time.\n");
                        }
                    }
                    else if (incomingMessage == "rematch")
                    {
                        logs.AppendText("Server: Your opponent requested a rematch. If you want to accept it please press the 'play again' button.");
                    }
                    else
                    {
                        string[] messageParts = incomingMessage.Split(' ');
                        if (messageParts[0] == "Welcome")
                        {
                            logs.AppendText("Server: " + incomingMessage + "\n");
                            string readyMessage = "ready";
                            Byte[] readyBuffer = Encoding.Default.GetBytes(readyMessage);
                            clientSocket.Send(readyBuffer);
                        }
                        else if (messageParts[0] == "your")
                        {
                            string symbol = messageParts[1];
                            int x = int.Parse(messageParts[3]);
                            int y = int.Parse(messageParts[2]);

                            gameBoardButtons[x, y].Text = symbol;

                            logs.AppendText("Server: Your move " + x + ", " + y + " was valid.\n");
                        }
                        else if (messageParts[0] == "their")
                        {
                            string symbol = messageParts[1];
                            int x = int.Parse(messageParts[3]);
                            int y = int.Parse(messageParts[2]);

                            gameBoardButtons[x, y].Text = symbol;

                            logs.AppendText("Server: Your opponent has played " + x + ", " + y + ".\n");
                        }
                        else if (messageParts[0] == "Invalid")
                        {
                            logs.AppendText("Server: " + incomingMessage + "\n");
                        }
                        else if (messageParts[0] == "The")
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                for (int j = 0; j < 3; j++)
                                {
                                    gameBoardButtons[i, j].Text = "";
                                }
                            }
                            logs.AppendText("Server: " + incomingMessage + "\n");
                        }
                    }
                }
                catch
                {
                    if (!terminating)
                    {
                        logs.AppendText("The server has disconnected.\n");
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

            if(message != "" && message.Length <= 64 && connected)
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

            if (connected)
            {
                string message = $"move {x} {y}";
                textBox_message.Text = message;
            }
        }

        private void button_playagain_Click(object sender, EventArgs e)
        {
            string message = "playagain";

            if (connected)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
                button_playagain.Enabled = false;
            }
        }
    }
}
