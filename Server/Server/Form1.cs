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

        List<Socket> waitlistSockets = new List<Socket>();
        List<bool> waitlistUpdated = new List<bool>() { false, false };
        List<String> moveList = new List<String>();

        bool gameOver = false;
        bool gameOnGoing = false;
        bool activeTurn = false;
        bool terminating = false;
        bool listening = false;
        bool activeMessage = false;
        string replaced = "";
        bool replacedReady = false;
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

            if (Int32.TryParse(textBox_port.Text, out serverPort))
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
            while (listening)
            {
                try
                {
                    Socket newClient = serverSocket.Accept();

                    string symbol = "";
                    string message = "";

                    if (clientSockets.Count < 2)
                    {
                        clientSockets.Add(newClient);
                        logs.AppendText("A client is connected.\n");
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
                        Byte[] buffer = Encoding.Default.GetBytes(message);
                        SendToClient(buffer, newClient);

                        Thread receiveThread = new Thread(() => Receive(newClient, symbol));
                        receiveThread.Start();
                    }
                    else if (waitlistSockets.Count < 2)
                    {
                        waitlistSockets.Add(newClient);
                        logs.AppendText("A client is added to waitlist.\n");

                        Thread receiveThread = new Thread(() => Receive(newClient, ""));
                        receiveThread.Start();

                        symbol = "";
                        message = "welcomewait";
                        activeMessage = true;
                        Byte[] buffer2 = Encoding.Default.GetBytes(message);
                        SendToClient(buffer2, newClient);

                        while (activeMessage) { }

                        foreach (string s in moveList)
                        {
                            activeMessage = true;
                            buffer2 = Encoding.Default.GetBytes(s);
                            SendToClient(buffer2, newClient);
                            while (activeMessage) { }
                        }
                    }
                    else
                    {
                        message = "";
                        symbol = "";
                    }

                    if (clientSockets.Count == 2)
                    {
                        if (gameOnGoing == false)
                        {
                            while (clientsReady.Count != 2) { }
                            if (clientsReady[0] == true && clientsReady[1] == true)
                            {
                                gameOnGoing = true;
                                Thread gameThread = new Thread(() => StartGame());
                                gameThread.Start();
                            }
                        }
                    }
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
        }

        private void SendToAllClients(byte[] buffer)
        {
            for (int i = clientSockets.Count - 1; i >= 0; i--)
            {
                SendToClient(buffer, clientSockets[i]);
            }

            for (int i = waitlistSockets.Count - 1; i >= 0; i--)
            {
                SendToClient(buffer, waitlistSockets[i]);
            }
        }

        private void SendToAllWatchers(byte[] buffer)
        {
            for (int i = waitlistSockets.Count - 1; i >= 0; i--)
            {
                SendToClient(buffer, waitlistSockets[i]);
            }
        }

        private void SendToClient(byte[] buffer, Socket client)
        {
            if (client.Connected)
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

        private void StartGame()
        {
            moveList.Clear();
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
            SendToAllClients(startBuffer);

            bool xTurn = true;

            while (!gameOver)
            {
                string playMessage = "play";
                Byte[] playBuffer = Encoding.Default.GetBytes(playMessage);
                string waitMessage = "wait";
                Byte[] waitBuffer = Encoding.Default.GetBytes(waitMessage);

                if (xTurn)
                {
                    activeTurn = true;
                    SendToClient(playBuffer, clientSockets[0]);
                    SendToClient(waitBuffer, clientSockets[1]);
                }
                else
                {
                    activeTurn = true;
                    SendToClient(playBuffer, clientSockets[1]);
                    SendToClient(waitBuffer, clientSockets[0]);
                }

                while (activeTurn)
                {
                    if (replaced != "")
                    {
                        while (clientsReady[0] == false || clientsReady[1] == false) { }

                        if (replaced == "X" && xTurn)
                        {
                            SendToClient(playBuffer, clientSockets[0]);
                            SendToClient(waitBuffer, clientSockets[1]);
                        }
                        else if (replaced == "O" && !xTurn)
                        {
                            SendToClient(playBuffer, clientSockets[1]);
                            SendToClient(waitBuffer, clientSockets[0]);
                        }
                        else if (replaced == "X" && !xTurn)
                        {
                            SendToClient(playBuffer, clientSockets[1]);
                            SendToClient(waitBuffer, clientSockets[0]);
                        }
                        else if (replaced == "O" && xTurn)
                        {
                            SendToClient(playBuffer, clientSockets[0]);
                            SendToClient(waitBuffer, clientSockets[1]);
                        }
                        replaced = "";
                    }
                }

                if (isGameDraw())
                {
                    logs.AppendText("Game ended in a draw.\n");

                    string drawMessage = "draw";
                    Byte[] drawBuffer = Encoding.Default.GetBytes(drawMessage);

                    SendToAllClients(drawBuffer);

                    clientsReady[0] = false;
                    clientsReady[1] = false;

                    gameOver = true;
                    gameOnGoing = false;
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
                        SendToClient(winBuffer, clientSockets[0]);
                        SendToClient(looseBuffer, clientSockets[1]);

                        string watcherMessage = "playerwin 1";
                        Byte[] watcherBuffer = Encoding.Default.GetBytes(watcherMessage);
                        SendToAllWatchers(watcherBuffer);
                    }
                    else
                    {
                        logs.AppendText("Player 2 wins!\n");
                        SendToClient(winBuffer, clientSockets[1]);
                        SendToClient(looseBuffer, clientSockets[0]);

                        string watcherMessage = "playerwin 2";
                        Byte[] watcherBuffer = Encoding.Default.GetBytes(watcherMessage);
                        SendToAllWatchers(watcherBuffer);
                    }

                    clientsReady[0] = false;
                    clientsReady[1] = false;

                    gameOver = true;
                    gameOnGoing = false;
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

        private void Receive(Socket thisClient, string symbol)
        {
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    Byte[] buffer = new Byte[64];
                    thisClient.Receive(buffer);

                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                    if (incomingMessage == "activeMessage")
                    {
                        activeMessage = false;
                    }
                    else if (incomingMessage == "replacedReady")
                    {
                        for (int i = 0; i < clientSockets.Count; i++)
                        {
                            if (clientSockets[i] == thisClient)
                            {
                                clientsReady[i] = true;
                            }
                        }
                    }
                    else if (incomingMessage == "replacedReady X")
                    {
                        logs.AppendText("A player from the waitlist has entered the game.\n");
                        while (clientsReady[1] == false) { }
                        string connectedMessage = "opponentConnected";
                        Byte[] connectedBuffer = Encoding.Default.GetBytes(connectedMessage);
                        SendToClient(connectedBuffer, clientSockets[1]);

                        symbol = "X";

                        for (int i = 0; i < clientSockets.Count; i++)
                        {
                            if (clientSockets[i] == thisClient)
                            {
                                clientsReady[i] = true;
                            }
                        }
                    }
                    else if (incomingMessage == "replacedReady O")
                    {
                        logs.AppendText("A player from the waitlist has entered the game.\n");
                        while (clientsReady[0] == false) { }
                        string connectedMessage = "opponentConnected";
                        Byte[] connectedBuffer = Encoding.Default.GetBytes(connectedMessage);
                        SendToClient(connectedBuffer, clientSockets[0]);

                        symbol = "O";

                        for (int i = 0; i < clientSockets.Count; i++)
                        {
                            if (clientSockets[i] == thisClient)
                            {
                                clientsReady[i] = true;
                            }
                        }
                    }
                    else
                    {
                        logs.AppendText("Client: " + incomingMessage + "\n");

                        string[] messageParts = incomingMessage.Split(' ');

                        if (messageParts[0] == "move")
                        {
                            int num = int.Parse(messageParts[1]);

                            string[] coords = convertCoord(num).Split(' ');

                            int y = int.Parse(coords[0]);
                            int x = int.Parse(coords[1]);

                            if (string.IsNullOrEmpty(gameBoard[x, y].Text))
                            {
                                gameBoard[x, y].Text = symbol;
                                string moveMessage1 = "your " + symbol + " " + num;
                                string moveMessage2 = "their " + symbol + " " + num;
                                string moveMessage3 = "move " + symbol + " " + num;
                                moveList.Add(moveMessage3);
                                Byte[] moveBuffer = Encoding.Default.GetBytes(moveMessage1);
                                thisClient.Send(moveBuffer);

                                foreach (Socket client in clientSockets)
                                {
                                    if (client != thisClient)
                                    {
                                        moveBuffer = Encoding.Default.GetBytes(moveMessage2);
                                        client.Send(moveBuffer);
                                    }
                                }

                                moveBuffer = Encoding.Default.GetBytes(moveMessage3);
                                SendToAllWatchers(moveBuffer);

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


                }
                catch
                {
                    if (!terminating)
                    {
                        logs.AppendText("A client has disconnected\n");
                    }

                    if (waitlistSockets.Count > 0)
                    {
                        for (int i = 0; i < clientSockets.Count; i++)
                        {
                            if (clientSockets[i] == thisClient)
                            {
                                clientsReady[i] = false;
                                replaced = symbol;
                                clientSockets.RemoveAt(i);
                                clientSockets.Insert(i, waitlistSockets[0]);
                                waitlistSockets.RemoveAt(0);
                                string replaceMessage = "replace " + symbol;
                                Byte[] replaceBuffer = Encoding.Default.GetBytes(replaceMessage);
                                SendToClient(replaceBuffer, clientSockets[i]);
                            }
                            else
                            {
                                clientsReady[i] = false;
                                string opponentMessage = "opponentDisconnected";
                                Byte[] opponentBuffer = Encoding.Default.GetBytes(opponentMessage);
                                SendToClient(opponentBuffer, clientSockets[i]);
                            }
                        }
                    }
                    thisClient.Close();
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
            if (message != "" && message.Length <= 64)
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
