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
            if (Int32.TryParse(textBox_port.Text, out portNum))
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

        private string convertCoord(int num)
        {
            if (num == 1)
            {
                return "0 0";
            }
            else if (num == 2)
            {
                return "1 0";
            }
            else if (num == 3)
            {
                return "2 0";
            }
            else if (num == 4)
            {
                return "0 1";
            }
            else if (num == 5)
            {
                return "1 1";
            }
            else if (num == 6)
            {
                return "2 1";
            }
            else if (num == 7)
            {
                return "0 2";
            }
            else if (num == 8)
            {
                return "1 2";
            }
            else
            {
                return "2 2";
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
                        logs.AppendText("Server: Your opponent requested a rematch. If you want to accept it please press the 'play again' button.\n");
                    }
                    else if (incomingMessage == "welcomewait")
                    {
                        logs.AppendText("Server: Welcome, you are in queue for a game. Please wait...\n");
                        string readyMessage = "activeMessage";
                        Byte[] readyBuffer = Encoding.Default.GetBytes(readyMessage);
                        clientSocket.Send(readyBuffer);
                    }
                    else if (incomingMessage == "opponentDisconnected")
                    {
                        logs.AppendText("Server: Your opponent was disconnected. Please wait until a new oppenent is connected...\n");
                        button_send.Enabled = false;
                        string readyMessage = "replacedReady";
                        Byte[] readyBuffer = Encoding.Default.GetBytes(readyMessage);
                        clientSocket.Send(readyBuffer);
                    }
                    else if (incomingMessage == "opponentConnected")
                    {
                        logs.AppendText("Server: A new opponent has connected.\n");
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
                        else if (messageParts[0] == "replace")
                        {
                            logs.AppendText("Server: A player has disconnected. You will be replacing them. You are " + messageParts[1] + ".\n");
                            string readyMessage = "replacedReady " + messageParts[1];
                            Byte[] readyBuffer = Encoding.Default.GetBytes(readyMessage);
                            clientSocket.Send(readyBuffer);
                        }
                        else if (messageParts[0] == "playerwin")
                        {
                            string m = "Player " + messageParts[1] + " won the game.";
                            logs.AppendText("Server: " + m + "\n");
                        }
                        else if (messageParts[0] == "your")
                        {
                            string symbol = messageParts[1];
                            int num = int.Parse(messageParts[2]);
                            string[] coords = convertCoord(num).Split(' ');
                            int x = int.Parse(coords[0]);
                            int y = int.Parse(coords[1]);

                            gameBoardButtons[x, y].Text = symbol;

                            logs.AppendText("Server: Your move " + num + " was valid.\n");
                        }
                        else if (messageParts[0] == "their")
                        {
                            string symbol = messageParts[1];
                            int num = int.Parse(messageParts[2]);
                            string[] coords = convertCoord(num).Split(' ');
                            int x = int.Parse(coords[0]);
                            int y = int.Parse(coords[1]);

                            gameBoardButtons[x, y].Text = symbol;

                            logs.AppendText("Server: Your opponent has played " + num + ".\n");
                        }
                        else if (messageParts[0] == "move")
                        {
                            string symbol = messageParts[1];
                            int num = int.Parse(messageParts[2]);
                            string[] coords = convertCoord(num).Split(' ');
                            int x = int.Parse(coords[0]);
                            int y = int.Parse(coords[1]);

                            gameBoardButtons[x, y].Text = symbol;

                            logs.AppendText("Server: A player has played " + num + ".\n");

                            string readyMessage = "activeMessage";
                            Byte[] readyBuffer = Encoding.Default.GetBytes(readyMessage);
                            clientSocket.Send(readyBuffer);
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

            if (message != "" && message.Length <= 64 && connected)
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
                string message = "";
                if (y == 0)
                {
                    if (x == 0)
                        message = "move 1"; //0 0
                    if (x == 1)
                        message = "move 2"; //1 0
                    if (x == 2)
                        message = "move 3"; //2 0

                }
                else if (y == 1)
                {
                    if (x == 0)
                        message = "move 4"; //0 1
                    if (x == 1)
                        message = "move 5"; //1 1
                    if (x == 2)
                        message = "move 6"; //2 1
                }
                else if (y == 2)
                {
                    if (x == 0)
                        message = "move 7";
                    if (x == 1)
                        message = "move 8";
                    if (x == 2)
                        message = "move 9";
                }
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
