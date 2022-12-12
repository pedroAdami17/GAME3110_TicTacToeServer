using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5492;

    LinkedList<PlayerAccount> playerAccounts;

    const int playerNameAndPassword = 1;

    string playerAccountFilePath;

    int playerInQueueID = -1;

    LinkedList<GameRoom> gameRooms;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccountFilePath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccounts.txt";
        playerAccounts = new LinkedList<PlayerAccount>();


        LoadPlayerAccount();

        foreach(PlayerAccount pa in playerAccounts)
        {
            Debug.Log(pa.name + " " + pa.password);
        }

        gameRooms = new LinkedList<GameRoom>();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');

        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToServerSignifiers.RegisterAccount)
        {
            string n = csv[1];
            string p = csv[2];
            bool nameIsInUse = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    nameIsInUse = true;
                }

            }
            if (nameIsInUse = true)
            {
                SendMessageToClient(ServerToClientSignifiers.RegisterFailed + "", id);
            }
            else
            {
                PlayerAccount newPlayerAccount = new PlayerAccount(n, p);
                playerAccounts.AddLast(newPlayerAccount);
                SendMessageToClient(ServerToClientSignifiers.RegisterComplete + "", id);

                SavePlayerAccount();
            }
        }
        else if (signifier == ClientToServerSignifiers.LoginToAccount)
        {
            string n = csv[1];
            string p = csv[2];
            bool nameFound = false;
            bool messageSentToClient = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    nameFound = true;
                    if (pa.password == p)
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginComplete + "", id);
                        messageSentToClient = true;
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                        messageSentToClient = true;
                    }
                }
            }

            if (!nameFound)
            {
                if (!messageSentToClient)
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                }
            }
        }else if(signifier == ClientToServerSignifiers.JoinGameRoomQueue)
        {
            Debug.Log("Waiting in queue");
            if(playerInQueueID == -1)
            {
                playerInQueueID = id;
            }
            else
            {
                GameRoom gr = new GameRoom(playerInQueueID, id);
                gameRooms.AddLast(gr);

                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID1);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID2);

                playerInQueueID = -1;
            }
        }else if(signifier == ClientToServerSignifiers.TicTacToeGame)
        {
            GameRoom gr = GetGameRoomWithClientID(id);

            if(gr != null)
            {
                if(gr.playerID1 == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "", gr.playerID2);
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.OpponentPlay + "", gr.playerID1);
                }
            }
        }

    }

    private void  SavePlayerAccount()
    {
        StreamWriter sw = new StreamWriter(playerAccountFilePath);

        foreach(PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(playerNameAndPassword + "," + pa.name + "," + pa.password);
        }
        sw.Close();
    }

    private void LoadPlayerAccount()
    {
        if(File.Exists(playerAccountFilePath))
        {
            StreamReader sr = new StreamReader(playerAccountFilePath);

            string line;

            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);

                if (signifier == playerNameAndPassword)
                {
                    PlayerAccount pa = new PlayerAccount(csv[1], csv[2]);
                    playerAccounts.AddLast(pa);
                }
            }

            sr.Close();
        }

    }

    private GameRoom GetGameRoomWithClientID(int id)
    {
        foreach(GameRoom gr in gameRooms)
        {
            if(gr.playerID1 == id || gr.playerID2 == id)
            {
                return gr;
            }
        }
        return null;
    }

}


public class PlayerAccount
{
    public string name, password;

    public PlayerAccount(string Name, string Password)
    {
        name = Name;
        password = Password;
    }
}

public class GameRoom
{
    public int playerID1, playerID2;

    public GameRoom(int PlayerID1, int PlayerID2)
    {
        playerID1 = PlayerID1;
        playerID2 = PlayerID2;
    }
}


public static class ClientToServerSignifiers
{
    public const int RegisterAccount = 1;
    public const int LoginToAccount = 2;
    public const int JoinGameRoomQueue = 3;
    public const int TicTacToeGame = 4;
}

public static class ServerToClientSignifiers
{
    public const int LoginComplete = 1;
    public const int LoginFailed = 2;

    public const int RegisterComplete = 3;
    public const int RegisterFailed = 4;

    public const int OpponentPlay = 5;
    
    public const int GameStart = 6;
}