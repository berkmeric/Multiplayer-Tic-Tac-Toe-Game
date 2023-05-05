using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace server
{
    public partial class Form1 : Form
    {

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<Socket> clientSockets = new List<Socket>();
        List<String> clientSymbols = new List<String>() { "X", "O" };
        List<bool> clientsReady = new List<bool>();

        bool gameOver = false;
        bool activeTurn = false;
        bool terminating = false;
        bool listening = false;
        Label[,] gameBoard = new Label[3, 3];
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
                    gameBoard[i, j] = new Label();
                    gameBoard[i, j].Text = "";
                    gameBoard[i, j].Location = new Point(10 + j * 100, 10 + i * 100);
                    gameBoard[i, j].Size = new Size(100, 100);
                    gameBoard[i, j].Font = new Font("Arial", 24, FontStyle.Bold);
                    gameBoard[i, j].TextAlign = ContentAlignment.MiddleCenter;
                    gameBoard[i, j].BorderStyle = BorderStyle.FixedSingle;
                    this.Controls.Add(gameBoard[i, j]);
                }
            }
        }

        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;

            if(Int32.TryParse(textBox_port.Text, out serverPort))
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                serverSocket.Bind(endPoint);
                serverSocket.Listen(3);

                listening = true;
                button_listen.Enabled = false;
                textBox_message.Enabled = true;
                button_send.Enabled = true;

                Thread acceptThread = new Thread(Accept);
                acceptThread.Start();

                logs.AppendText("Started listening on port: " + serverPort + "\n");
            }
            else
            {
                logs.AppendText("Please check port number \n");
            }
        }

        private void Accept()
        {
            while(listening && clientSockets.Count != clientSymbols.Count)
            {
                try
                {
                    Socket newClient = serverSocket.Accept();
                    clientSockets.Add(newClient);
                    logs.AppendText("A client is connected.\n");

                    string symbol;

                    string message;
                    if (clientSockets.Count == 1)
                    {
                        message = "Welcome player 1. You are X!";
                        symbol = "X";
                    }
                    else if (clientSockets.Count == 2)
                    {
                        message = "Welcome player 2. You are O!";
                        symbol = "O";
                    }
                    else
                    {
                        message = "";
                        symbol = "";
                    }

                    if (message != "" && message.Length <= 64)
                    {
                        Byte[] buffer = Encoding.Default.GetBytes(message);
                        try
                        {
                            newClient.Send(buffer);
                        }
                        catch
                        {
                            logs.AppendText("There is a problem! Check the connection...\n");
                            terminating = true;
                            textBox_message.Enabled = false;
                            button_send.Enabled = false;
                            textBox_port.Enabled = true;
                            button_listen.Enabled = true;
                            serverSocket.Close();
                        }
                    }

                    Thread receiveThread = new Thread(() => Receive(newClient, symbol));
                    receiveThread.Start();
                }
                catch
                {
                    if (terminating)
                    {
                        listening = false;
                    }
                    else
                    {
                        logs.AppendText("The socket stopped working.\n");
                    }

                }
            }

            if (clientSockets.Count == clientSymbols.Count)
            {
                while (clientsReady.Count != 2){}
                if (clientsReady[0] == true && clientsReady[1] == true)
                {
                    StartGame();
                }
            }
        }

        private void StartGame()
        {
            gameOver = false;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    gameBoard[i, j].Text = "";
                }
            }

            logs.AppendText("The game is starting...\n");
            string startMessage = "The game is starting...";
            Byte[] startBuffer = Encoding.Default.GetBytes(startMessage);
            foreach (Socket client in clientSockets)
            {
                try
                {
                    client.Send(startBuffer);
                }
                catch
                {
                    logs.AppendText("There is a problem! Check the connection...\n");
                    terminating = true;
                    textBox_message.Enabled = false;
                    button_send.Enabled = false;
                    textBox_port.Enabled = true;
                    button_listen.Enabled = true;
                    serverSocket.Close();
                }

            }

            bool xTurn = true;
            
            while (!gameOver)
            {
                string playMessage = "play";
                Byte[] playBuffer = Encoding.Default.GetBytes(playMessage);
                string waitMessage = "wait";
                Byte[] waitBuffer = Encoding.Default.GetBytes(waitMessage);

                Socket XClient = clientSockets[0];
                Socket OClient = clientSockets[1];

                if (xTurn)
                {
                    activeTurn = true;
                    try
                    {
                        XClient.Send(playBuffer);
                        OClient.Send(waitBuffer);
                    }
                    catch
                    {
                        logs.AppendText("There is a problem! Check the connection...\n");
                        terminating = true;
                        textBox_message.Enabled = false;
                        button_send.Enabled = false;
                        textBox_port.Enabled = true;
                        button_listen.Enabled = true;
                        serverSocket.Close();
                    }
                }
                else
                {
                    activeTurn = true;
                    try
                    {
                        OClient.Send(playBuffer);
                        XClient.Send(waitBuffer);
                    }
                    catch
                    {
                        logs.AppendText("There is a problem! Check the connection...\n");
                        terminating = true;
                        textBox_message.Enabled = false;
                        button_send.Enabled = false;
                        textBox_port.Enabled = true;
                        button_listen.Enabled = true;
                        serverSocket.Close();
                    }
                }

                while (activeTurn) { }

                if (isGameDraw())
                {
                    logs.AppendText("Game ended in a draw.\n");
                    gameOver = true;
                    string drawMessage = "draw";
                    Byte[] drawBuffer = Encoding.Default.GetBytes(drawMessage);

                    XClient.Send(drawBuffer);
                    OClient.Send(drawBuffer);

                    clientsReady[0] = false;
                    clientsReady[1] = false;
                }
                else if (isGameWon())
                {
                    string winMessage = "win";
                    Byte[] winBuffer = Encoding.Default.GetBytes(winMessage);
                    string looseMessage = "loose";
                    Byte[] looseBuffer = Encoding.Default.GetBytes(looseMessage);
                    if (xTurn)
                    {
                        logs.AppendText("Player 1 wins!\n");
                        XClient.Send(winBuffer);
                        OClient.Send(looseBuffer);
                    }
                    else
                    {
                        logs.AppendText("Player 2 wins!\n");
                        OClient.Send(winBuffer);
                        XClient.Send(looseBuffer);
                    }
                    gameOver = true;
                    clientsReady[0] = false;
                    clientsReady[1] = false;
                }

                xTurn = !xTurn;
            }
        }

        private bool isGameDraw()
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (gameBoard[i, j].Text == "")
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool isGameWon()
        {
            if (gameBoard[0, 0].Text != "")
            {
                string targetSymbol = gameBoard[0, 0].Text;
                if (gameBoard[0, 1].Text == targetSymbol && gameBoard[0, 2].Text == targetSymbol)
                {
                    return true;
                }
                else if (gameBoard[1, 0].Text == targetSymbol && gameBoard[2, 0].Text == targetSymbol)
                {
                    return true;
                }
                else if (gameBoard[1, 1].Text == targetSymbol && gameBoard[2, 2].Text == targetSymbol)
                {
                    return true;
                }
            }
            if (gameBoard[1, 0].Text != "")
            {
                string targetSymbol = gameBoard[1, 0].Text;
                if (gameBoard[1, 1].Text == targetSymbol && gameBoard[1, 2].Text == targetSymbol)
                {
                    return true;
                }
            }
            if (gameBoard[2, 0].Text != "")
            {
                string targetSymbol = gameBoard[2, 0].Text;
                if (gameBoard[2, 1].Text == targetSymbol && gameBoard[2, 2].Text == targetSymbol)
                {
                    return true;
                }
                else if (gameBoard[1, 1].Text == targetSymbol && gameBoard[0, 2].Text == targetSymbol)
                {
                    return true;
                }
            }
            if (gameBoard[0, 1].Text != "")
            {
                string targetSymbol = gameBoard[0, 1].Text;
                if (gameBoard[1, 1].Text == targetSymbol && gameBoard[2, 1].Text == targetSymbol)
                {
                    return true;
                }
            }
            if (gameBoard[0, 2].Text != "")
            {
                string targetSymbol = gameBoard[0, 2].Text;
                if (gameBoard[1, 2].Text == targetSymbol && gameBoard[2, 2].Text == targetSymbol)
                {
                    return true;
                }
            }

            return false;
        }

        private void Receive(Socket thisClient, string symbol) // updated
        {
            bool connected = true;

            while(connected && !terminating)
            {
                try
                {
                    Byte[] buffer = new Byte[64];
                    thisClient.Receive(buffer);

                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                    logs.AppendText("Client: " + incomingMessage + "\n");

                    string[] messageParts = incomingMessage.Split(' ');

                    if (messageParts[0] == "move")
                    {
                        int y = int.Parse(messageParts[1]);
                        int x = int.Parse(messageParts[2]);

                        if (string.IsNullOrEmpty(gameBoard[x,y].Text))
                        {
                            gameBoard[x, y].Text = symbol;
                            string moveMessage1 = "your " + symbol + " " + x + " " + y;
                            string moveMessage2 = "their " + symbol + " " + x + " " + y;
                            Byte[] moveBuffer = Encoding.Default.GetBytes(moveMessage1);
                            thisClient.Send(moveBuffer);

                            foreach(Socket client in clientSockets)
                            {
                                if (client != thisClient)
                                {
                                    moveBuffer = Encoding.Default.GetBytes(moveMessage2);
                                    client.Send(moveBuffer);
                                }
                            }

                            activeTurn = false;
                        }
                        else
                        {
                            string invalidMoveMessage = "Invalid move try again";
                            Byte[] invalidBuffer = Encoding.Default.GetBytes(invalidMoveMessage);
                            thisClient.Send(invalidBuffer);
                        }
                    }
                    else if (messageParts[0] == "ready")
                    {
                        clientsReady.Add(true);
                    }
                    else if (messageParts[0] == "playagain")
                    {
                        for (int i = 0; i < clientSockets.Count; i++)
                        {
                            if (clientSockets[i] == thisClient)
                            {
                                clientsReady[i] = true;
                            }
                        }

                        if (clientsReady[0] && clientsReady[1])
                        {
                            for (int i = 0; i < clientSockets.Count; i++)
                            {
                                Socket client = clientSockets[i];
                                string s = clientSymbols[i];
                                Thread receiveThread = new Thread(() => Receive(client, s));
                                receiveThread.Start();
                            }

                            StartGame();
                        }
                        else if (!clientsReady[0] && clientsReady[1])
                        {
                            Socket c = clientSockets[0];
                            string rematchMessage = "rematch";
                            Byte[] rematchBuffer = Encoding.Default.GetBytes(rematchMessage);
                            c.Send(rematchBuffer);
                        }
                        else if (clientsReady[0] && !clientsReady[1])
                        {
                            Socket c = clientSockets[1];
                            string rematchMessage = "rematch";
                            Byte[] rematchBuffer = Encoding.Default.GetBytes(rematchMessage);
                            c.Send(rematchBuffer);
                        }
                    }
                }
                catch
                {
                    if(!terminating)
                    {
                        logs.AppendText("A client has disconnected\n");
                    }
                    thisClient.Close();
                    clientSockets.Remove(thisClient);
                    connected = false;
                }
            }
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            Environment.Exit(0);
        }

        private void button_send_Click(object sender, EventArgs e)
        {
            string message = textBox_message.Text;
            if(message != "" && message.Length <= 64)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                foreach (Socket client in clientSockets)
                {
                    try
                    {
                        client.Send(buffer);
                    }
                    catch
                    {
                        logs.AppendText("There is a problem! Check the connection...\n");
                        terminating = true;
                        textBox_message.Enabled = false;
                        button_send.Enabled = false;
                        textBox_port.Enabled = true;
                        button_listen.Enabled = true;
                        serverSocket.Close();
                    }

                }
            }
        }
    }
}
