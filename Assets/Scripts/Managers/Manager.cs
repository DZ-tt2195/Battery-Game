using System.Collections;
using System.Collections.Generic;
using MyBox;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;
using Photon.Realtime;
using System.Linq;
using System.Reflection;

public class Manager : UndoSource
{

#region Variables

    public static Manager instance;

    [Foldout("Text", true)]
    public TMP_Text instructions;
    public Transform deck;
    public Transform discard;
    public Transform actions;
    public Transform events;

    [Foldout("Animation", true)]
    [ReadOnly] public float opacity = 1;
    [ReadOnly] public bool decrease = true;
    [ReadOnly] public bool gameOn = false;

    [Foldout("Lists", true)]
    [ReadOnly] public int turnNumber;
    [ReadOnly] public List<Player> playersInOrder = new();
    [ReadOnly] public List<Card> listOfActions = new();
    [ReadOnly] public List<Card> listOfEvents = new();
    [ReadOnly] public Dictionary<string, MethodInfo> dictionary = new();

    [Foldout("Ending", true)]
    [SerializeField] Transform endScreen;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] Button quitGame;

    #endregion

#region Setup

    private void Awake()
    {
        instance = this;
        pv = GetComponent<PhotonView>();
    }

    protected override void AddToMethodDictionary(string methodName)
    {
        MethodInfo method = typeof(Manager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    private void FixedUpdate()
    {
        if (decrease)
            opacity -= 0.05f;
        else
            opacity += 0.05f;
        if (opacity < 0 || opacity > 1)
            decrease = !decrease;
    }

    void Start()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Instantiate(CarryVariables.instance.playerPrefab.name, new Vector3(-10000, -10000, 0), new Quaternion());
            StartCoroutine(WaitForPlayers());
        }
        else
        {
            Player solitairePlayer = Instantiate(CarryVariables.instance.playerPrefab, new Vector3(-10000, -10000, 0), new Quaternion());
            solitairePlayer.name = "Solitaire";

            GetPlayers();
            CreateRobots();
            CreateActions();
            StartCoroutine(PlayUntilFinish());
        }
    }

    IEnumerator WaitForPlayers()
    {
        instructions.text = "Waiting...";
        while (PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            yield return null;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            yield return new WaitForSeconds(0.5f);
            GetPlayers();

            CreateRobots();
            CreateActions();
            StartCoroutine(PlayUntilFinish());
        }
    }

    void GetPlayers()
    {
        List<Player> listOfPlayers = FindObjectsOfType<Player>().ToList();
        int counter = 0;
        while (listOfPlayers.Count > 0)
        {
            int randomRemove = Random.Range(0, listOfPlayers.Count);
            MultiFunction(nameof(AddPlayer), RpcTarget.All, new object[2] { listOfPlayers[randomRemove].name, counter });
            listOfPlayers.RemoveAt(randomRemove);
            counter++;
        }
    }

    [PunRPC]
    void AddPlayer(string name, int position)
    {
        Player nextPlayer = GameObject.Find(name).GetComponent<Player>();
        playersInOrder.Insert(position, nextPlayer);
        nextPlayer.AssignInfo(position);
    }

    void CreateRobots()
    {
        for (int i = 0; i < DownloadSheets.instance.robotData.Count; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                Card nextCard = null;
                if (PhotonNetwork.IsConnected)
                {
                    nextCard = PhotonNetwork.Instantiate(CarryVariables.instance.robotPrefab.name, new Vector3(-10000, -10000), new Quaternion()).GetComponent<Card>();
                    nextCard.pv.RPC("GetRobotFile", RpcTarget.All, i);
                }
                else
                {
                    nextCard = Instantiate(CarryVariables.instance.robotPrefab, new Vector3(-10000, -10000), new Quaternion());
                    nextCard.GetRobotFile(i);
                }
            }
        }
        deck.Shuffle();
        foreach (Player player in playersInOrder)
        {
            player.MultiFunction(nameof(player.RequestDraw), RpcTarget.MasterClient, new object[2] { 2, 0 });
            player.CoinRPC(this, 4, -1);
        }
    }

    void CreateActions()
    {
        for (int i = 0; i < DownloadSheets.instance.mainActionData.Count; i++)
        {
            Card nextCard = null;
            if (PhotonNetwork.IsConnected)
            {
                nextCard = PhotonNetwork.Instantiate(CarryVariables.instance.actionPrefab.name, new Vector3(-10000, -10000), new Quaternion()).GetComponent<Card>();
                nextCard.pv.RPC("GetActionFile", RpcTarget.All, i);
            }
            else
            {
                nextCard = Instantiate(CarryVariables.instance.actionPrefab, new Vector3(-10000, -10000), new Quaternion());
                nextCard.GetActionFile(i);
            }
        }
    }

    #endregion

#region Gameplay

    IEnumerator PlayUntilFinish()
    {
        gameOn = true;

        for (int j = 1; j <= 10; j++)
        {
            MultiFunction(nameof(UpdateTurnNumber), RpcTarget.All, new object[1] { j });
            foreach (Player player in playersInOrder)
            {
                player.TakeTurnRPC(j);
                while (player.myTurn)
                    yield return null;
            }
        }

        MultiFunction(nameof(DisplayEnding), RpcTarget.All, new object[1] { -1 });
    }

    [PunRPC]
    void UpdateTurnNumber(int number)
    {
        turnNumber = number;
        Log.instance.AddText("");
        Log.instance.AddText($"ROUND {turnNumber}");
    }

    #endregion

#region Game End

    [PunRPC]
    public void DisplayEnding(int resignPosition)
    {
        StopCoroutine(nameof(PlayUntilFinish));
        endScreen.gameObject.SetActive(true);
        quitGame.onClick.AddListener(Leave);

        Popup[] allPopups = FindObjectsOfType<Popup>();
        foreach (Popup popup in allPopups)
            Destroy(popup.gameObject);

        List<Player> playerScoresInOrder = playersInOrder.OrderByDescending(player => player.CalculateScore()).ToList();
        int nextPlacement = 1;
        scoreText.text = "";

        Log.instance.AddText("");
        Log.instance.AddText("The game has ended.");
        Player resignPlayer = null;
        if (resignPosition >= 0)
        {
            resignPlayer = playersInOrder[resignPosition];
            Log.instance.AddText($"{resignPlayer.name} has resigned.");
        }

        for (int i = 0; i<playerScoresInOrder.Count; i++)
        {
            Player player = playerScoresInOrder[i];
            if (player != resignPlayer)
            {
                scoreText.text += $"{nextPlacement}: {player.name}: {player.CalculateScore()} Pos Crown\n";
                if (i == 0 || playerScoresInOrder[i - 1].CalculateScore() != player.CalculateScore())
                    nextPlacement++;
            }
        }

        if (resignPlayer != null)
            scoreText.text += $"\nResigned: {resignPlayer.name}: {resignPlayer.CalculateScore()} Pos Crown";
        scoreText.text = KeywordTooltip.instance.EditText(scoreText.text);
    }

    void Leave()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene("1. Lobby");
        }
        else
        {
            SceneManager.LoadScene("0. Loading");
        }
    }

#endregion

}
