using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;
using Photon.Realtime;
using System.Linq;
using MyBox;
using System.Reflection;

[RequireComponent(typeof(PhotonView))]
public class Player : UndoSource
{

#region Variables

    [Foldout("Prefabs", true)]
        [SerializeField] Card junkPrefab;
        [SerializeField] Button playerButtonPrefab;

    [Foldout("Misc", true)]
        Canvas canvas;
        [ReadOnly] public int coins;
        [ReadOnly] public int negCrowns;
        bool myTurn;
        [ReadOnly] public int playerPosition;

    [Foldout("UI", true)]
        [ReadOnly] public List<Card> listOfHand = new List<Card>();
        [SerializeField] Transform cardhand;
        [ReadOnly] public List<Card> listOfPlay = new List<Card>();
        [SerializeField] Transform cardplay;
        TMP_Text buttonText;
        Transform storePlayers;
        Button resignButton;

    [Foldout("Choices", true)]
        [ReadOnly] public int choice;
        [ReadOnly] public Card chosenCard;
        [ReadOnly] public Card lastUsedAction;
        [ReadOnly] public int ignoreInstructions = 0;

    #endregion

#region Setup

    private void Awake()
    {
        pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && pv.AmOwner)
            pv.Owner.NickName = PlayerPrefs.GetString("Online Username");

        canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        resignButton = GameObject.Find("Resign Button").GetComponent<Button>();
    }

    void ResignTime()
    {
        Manager.instance.MultiFunction(nameof(Manager.instance.DisplayEnding), RpcTarget.All, new object[1] { this.playerPosition });
    }

    private void Start()
    {
        if (PhotonNetwork.IsConnected)
            this.name = pv.Owner.NickName;
    }

    public void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!methodDictionary.ContainsKey(methodName))
            AddToMethodDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(methodDictionary[methodName].Name, affects, parameters);
        else
            methodDictionary[methodName].Invoke(this, parameters);
    }

    public IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!methodDictionary.ContainsKey(methodName))
            AddToMethodDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(methodDictionary[methodName].Name, affects, parameters);
        else
            yield return (IEnumerator)methodDictionary[methodName].Invoke(this, parameters);
    }

    protected override void AddToMethodDictionary(string methodName)
    {
        MethodInfo method = typeof(Player).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            methodDictionary.Add(methodName, method);
    }

    internal void AssignInfo(int position)
    {
        this.playerPosition = position;
        resignButton.onClick.AddListener(ResignTime);
        storePlayers = GameObject.Find("Store Players").transform;
        this.transform.SetParent(storePlayers);
        this.transform.localPosition = new Vector3(2500 * this.playerPosition, 0, 0);

        Button newButton = Instantiate(playerButtonPrefab, Vector3.zero, new Quaternion());
        newButton.transform.SetParent(this.transform.parent.parent);
        newButton.transform.localPosition = new(-1100, 400 - (200 * playerPosition));
        buttonText = newButton.transform.GetChild(0).GetComponent<TMP_Text>();
        newButton.onClick.AddListener(MoveScreen);

        if (!PhotonNetwork.IsConnected || pv.IsMine)
        {
            MoveScreen();
        }
    }

    [PunRPC]
    void UpdateButton()
    {
        if (buttonText != null)
        {
            buttonText.text = $"{this.name}\n{listOfHand.Count} Card, {coins} Coin, -{negCrowns} Neg Crown";
            buttonText.text = KeywordTooltip.instance.EditText(buttonText.text);
        }
    }

    void MoveScreen()
    {
        storePlayers.localPosition = new Vector3(-2500 * this.playerPosition, 0, 0);
    }

    public override IEnumerator UndoCommand(UndoStep step)
    {
        if (!methodDictionary.ContainsKey(step.instruction))
            AddToMethodDictionary(step.instruction);

        yield return ((IEnumerator)methodDictionary[step.instruction].Invoke(this, new object[2] { step, true }));
    }

    #endregion

#region Cards in Hand

    public void DiscardRPC(Card card, int logged)
    {
        Log.instance.AddUndoStep(this, this, nameof(SendDiscard));
        Log.instance.AddCardToUndo(card);
        MultiFunction(nameof(SendDiscard), RpcTarget.All, new object[2] { logged, false });
    }

    [PunRPC]
    void SendDiscard(int logged, bool undo)
    {
        UndoStep step = Log.instance.GetNewStep();
        if (undo)
        {
            StartInHand(step.cardsToRemember[0], 0f);
        }
        else
        {
            Card discardMe = step.cardsToRemember[0];
            listOfHand.Remove(discardMe);
            discardMe.transform.SetParent(Manager.instance.discard);
            StartCoroutine(discardMe.MoveCard(new Vector2(-2000, -330), new Vector3(0, 0, 0), 0.3f));
            Log.instance.AddText($"{this.name} discards {discardMe.name}.", logged);
        }
        SortHand();
    }

    [PunRPC]
    public void RequestDraw(int cardsToDraw, int logged)
    {
        int[] cardIDs = new int[cardsToDraw];
        Card[] listOfDraw = new Card[cardsToDraw];

        for (int i = 0; i < cardsToDraw; i++)
        {
            if (Manager.instance.deck.childCount == 0)
            {
                Manager.instance.discard.Shuffle();
                while (Manager.instance.discard.childCount > 0)
                    Manager.instance.discard.GetChild(0).SetParent(Manager.instance.deck);
            }

            PhotonView x = Manager.instance.deck.GetChild(i).GetComponent<PhotonView>();
            cardIDs[i] = x.ViewID;
            Card y = Manager.instance.deck.GetChild(i).GetComponent<Card>();
            listOfDraw[i] = y;
        }

        Log.instance.AddUndoStep(this, this, nameof(AddToHand));
        foreach (Card card in listOfDraw)
            Log.instance.AddCardToUndo(card);

        MultiFunction(nameof(AddToHand), RpcTarget.All, new object[2] { logged, false });
    }

    [PunRPC]
    void AddToHand(int logged, bool undo)
    {
        UndoStep step = Log.instance.GetNewStep();
        if (undo)
        {
            foreach (Card card in step.cardsToRemember.AsEnumerable().Reverse())
            {
                card.transform.SetParent(Manager.instance.deck);
                card.transform.localPosition = new(-10000, -10000);
                card.transform.SetAsFirstSibling();
                listOfHand.Remove(card);
            }
        }
        else
        {
            string cardList = "";
            for (int i = 0; i < step.cardsToRemember.Count; i++)
            {
                Card newCard = step.cardsToRemember[i];
                StartInHand(newCard, 0.3f);
                cardList += $"{newCard.name}{(i < step.cardsToRemember.Count - 1 ? ", " : ".")}";
            }

            if (!PhotonNetwork.IsConnected || this.pv.AmOwner)
                Log.instance.AddText($"{this.name} draws {cardList}", logged);
            else
                Log.instance.AddText($"{this.name} draws {step.cardsToRemember.Count} Card.");
        }
        SortHand();
    }

    void StartInHand(Card newCard, float time)
    {
        newCard.transform.SetParent(this.cardhand);
        newCard.transform.localPosition = new Vector2(0, -1100);
        newCard.cg.alpha = 0;
        listOfHand.Add(newCard);

        if (!PhotonNetwork.IsConnected || this.pv.AmOwner)
            StartCoroutine(newCard.RevealCard(time));
    }

    public void SortHand()
    {
        float firstCalc = Mathf.Round(canvas.transform.localScale.x * 4) / 4f;
        float multiplier = firstCalc / 0.25f;
        listOfHand = listOfHand.OrderBy(card => card.dataFile.coinCost).ToList();
        UpdateButton();

        for (int i = 0; i < listOfHand.Count; i++)
        {
            Card nextCard = listOfHand[i];
            nextCard.transform.SetSiblingIndex(i);
            float startingX = (listOfHand.Count > 7) ? (-250 - (150 * multiplier)) : (listOfHand.Count - 1) * (-50 - 25 * multiplier);
            float difference = (listOfHand.Count > 7) ? (-250 - (150 * multiplier)) * -2 / (listOfHand.Count - 1) : 100 + (50 * multiplier);

            Vector2 newPosition = new(startingX + difference * i, -535 * canvas.transform.localScale.x);
            StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
        }

        if (!PhotonNetwork.IsConnected || this.pv.AmOwner)
        {
            foreach (Card card in listOfHand)
                StartCoroutine(card.RevealCard(0.25f));
        }
    }

    #endregion

#region Cards in Play

    public void SortPlay()
    {
        float firstCalc = Mathf.Round(canvas.transform.localScale.x * 4) / 4f;
        float multiplier = firstCalc / 0.25f;
        UpdateButton();

        for (int i = 0; i<6; i++)
        {
            try
            {
                Card nextCard = listOfPlay[i];
                nextCard.transform.SetSiblingIndex(i);
                Vector2 newPosition = new(-800 + (62.5f * multiplier * i), 175 * canvas.transform.localScale.x);
                StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
            }
            catch
            {
                break;
            }
        }
        for (int i = 0; i < 6; i++)
        {
            try
            {
                Card nextCard = listOfPlay[i+6];
                nextCard.transform.SetSiblingIndex(i+6);
                Vector2 newPosition = new(-800 + (62.5f * multiplier * i), -185 * canvas.transform.localScale.x);
                StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.3f));
            }
            catch
            {
                break;
            }
        }

        foreach (Card card in listOfPlay)
            StartCoroutine(card.RevealCard(0.25f));
    }

    public IEnumerator ChooseCardToPlay(List<Card> cardsToPlay, int logged)
    {
        if (cardsToPlay.Count == 0)
            yield break;

        Manager.instance.instructions.text = "Choose a card to play.";
        yield return ChooseCard(cardsToPlay, true);

        if (chosenCard != null)
        {
            Card cardToPlay = chosenCard;

            if (PhotonNetwork.IsConnected)
                pv.RPC(nameof(PhotonViewToPlay), RpcTarget.All, cardToPlay.pv.ViewID, logged);
            else
                AddToPlayArea(cardToPlay, logged);

            cardToPlay.MultiFunction(nameof(cardToPlay.AddBatteries), RpcTarget.All, new object[3] { this.playerPosition, cardToPlay.dataFile.startingBatteries, logged });
        }
    }

    void AddToPlayArea(Card card, int logged)
    {
        if (card == null)
        {
            Log.instance.AddText($"{this.name} doesn't play anything.", logged);
            SortHand();
            SortPlay();
        }
        else
        {
            card.name = card.name.Replace("(Clone)", "");
            card.cg.alpha = 1;

            listOfHand.Remove(card);
            listOfPlay.Add(card);
            card.transform.SetParent(cardplay);

            //LoseCoin(card.dataFile.coinCost, logged);
            Log.instance.AddText($"{this.name} plays {card.name}.", logged);

            SortHand();
            SortPlay();
        }
    }

    [PunRPC]
    void PhotonViewToPlay(int cardID, int logged)
    {
        AddToPlayArea(PhotonView.Find(cardID).GetComponent<Card>(), logged);
    }

    #endregion

#region Resources

    public void CoinRPC(int number, int logged)
    {
        Log.instance.AddUndoStep(this, this, nameof(ChangeCoin));
        Log.instance.MultiFunction(nameof(Log.instance.AddNumber), RpcTarget.All, new object[1] { number });
        MultiFunction(nameof(ChangeCoin), RpcTarget.All, new object[2] { logged, false });
    }

    [PunRPC]
    void ChangeCoin(int logged, bool undo)
    {
        UndoStep step = Log.instance.GetNewStep();
        if (undo)
        {
            this.coins -= step.numberToRemember;
        }
        else if (step.numberToRemember != 0)
        {
            int numberToChange = (this.coins + step.numberToRemember < 0) ? this.coins : this.coins + step.numberToRemember;
            this.coins += numberToChange;
            Log.instance.MultiFunction(nameof(Log.instance.AddNumber), RpcTarget.All, new object[1] { numberToChange });

            if (numberToChange > 0)
                Log.instance.AddText($"{this.name} gains {numberToChange} Coin.", logged);
            else
                Log.instance.AddText($"{this.name} loses {numberToChange} Coin.", logged);
        }
        UpdateButton();
    }

    public void CrownRPC(int number, int logged)
    {
        Log.instance.AddUndoStep(this, this, nameof(ChangeCrown));
        Log.instance.MultiFunction(nameof(Log.instance.AddNumber), RpcTarget.All, new object[1] { number });
        MultiFunction(nameof(ChangeCrown), RpcTarget.All, new object[2] { logged, false });
    }

    [PunRPC]
    void ChangeCrown(int logged, bool undo)
    {
        UndoStep step = Log.instance.GetNewStep();
        if (undo)
        {
            this.negCrowns -= step.numberToRemember;
        }
        else if (step.numberToRemember != 0)
        {
            int numberToChange = (this.coins + step.numberToRemember < 0) ? this.negCrowns : this.negCrowns + step.numberToRemember;
            this.negCrowns += numberToChange;
            Log.instance.MultiFunction(nameof(Log.instance.AddNumber), RpcTarget.All, new object[1] { numberToChange });

            if (numberToChange > 0)
                Log.instance.AddText($"{this.name} takes -{numberToChange} Neg Crown.", logged);
            else
                Log.instance.AddText($"{this.name} removes -{numberToChange} Neg Crown.", logged);
        }
        UpdateButton();
    }

    public int CalculateScore()
    {
        int score = negCrowns;
        foreach (Card card in listOfPlay)
            score += card.dataFile.scoringCrowns;
        return score;
    }

    #endregion

#region Turn/Decisions

    public IEnumerator TakeTurnRPC(int turnNumber)
    {
        StartCoroutine(MultiEnumerator(nameof(TakeTurn), RpcTarget.All, new object[1] {turnNumber} ));
        myTurn = true;
        while (myTurn)
            yield return null;
    }

    [PunRPC]
    IEnumerator TakeTurn(int turnNumber)
    {
        Log.instance.AddText($"");
        Log.instance.AddText($"Turn {turnNumber} - {this.name}");
        Manager.instance.instructions.text = $"Waiting for {this.name}";
        yield return null;

        if (!PhotonNetwork.IsConnected || this.pv.IsMine)
        {
            //choosing actions
            Card actionToUse = null;
                Popup actionPopup = Instantiate(CarryVariables.instance.cardPopup);
                actionPopup.transform.SetParent(this.transform);
                actionPopup.StatsSetup("Actions", Vector3.zero);
                Manager.instance.instructions.text = "Choose an action.";

                foreach (Card action in Manager.instance.listOfActions)
                    actionPopup.AddCardButton(action, 1);
                yield return actionPopup.WaitForChoice();

                actionToUse = actionPopup.chosenCard;
                Destroy(actionPopup.gameObject);

            Log.instance.MultiFunction(nameof(Log.instance.AddText), RpcTarget.All, new object[2] { $"{this.name} uses {actionToUse.name}.", 0 });
            yield return actionToUse.PlayInstructions(this, 0);

            yield return ResolveRobots();

            MultiFunction(nameof(EndTurn), RpcTarget.All, new object[1] { Manager.instance.listOfActions.FindIndex(action => action == actionToUse) });
        }
    }

    IEnumerator ResolveRobots()
    {
        List<Card> resolvedRobots = new();
        List<Card> availableRobots = new();
        do
        {
            availableRobots = listOfPlay.Where(robot => robot.batteries > 0 && !resolvedRobots.Contains(robot)).ToList();
            yield return ChooseCard(availableRobots, false);

            if (chosenCard != null)
            {
                resolvedRobots.Add(chosenCard);
                chosenCard.MultiFunction(nameof(chosenCard.RemoveBatteries), RpcTarget.All, new object[3] { this.playerPosition, 1, 0 });
                yield return chosenCard.PlayInstructions(this, 1);
            }

        } while (availableRobots.Count > 0);
    }

    [PunRPC]
    void EndTurn(int chosenAction)
    {
        lastUsedAction = Manager.instance.listOfActions[chosenAction];
        myTurn = false;
        ignoreInstructions = Mathf.Max(0, ignoreInstructions - 1);
    }

    public IEnumerator ChooseCard(List<Card> possibleCards, bool optional)
    {
        choice = -10;
        chosenCard = null;

        if (possibleCards.Count > 0)
        {
            Popup popup = null;

            if (optional)
            {
                popup = Instantiate(CarryVariables.instance.textPopup);
                popup.transform.SetParent(GameObject.Find("Canvas").transform);
                popup.StatsSetup("Decline?", Vector3.zero);
                popup.AddTextButton("Decline");
                StartCoroutine(popup.WaitForChoice());
            }

            for (int i = 0; i < possibleCards.Count; i++)
            {
                Card nextCard = possibleCards[i];
                int buttonNumber = i;

                nextCard.button.onClick.RemoveAllListeners();
                nextCard.button.interactable = true;
                nextCard.button.onClick.AddListener(() => ReceiveChoice(buttonNumber));
                nextCard.border.gameObject.SetActive(true);
            }

            if (possibleCards.Count == 1 && !optional)
            {
                choice = 0;
            }
            else
            {
                while (choice == -10)
                {
                    yield return null;
                    if (optional && popup.chosenButton > -10)
                        break;
                }
            }

            if (popup != null)
                Destroy(popup.gameObject);

            chosenCard = (choice >= 0) ? possibleCards[choice] : null;

            for (int i = 0; i < possibleCards.Count; i++)
            {
                Card nextCard = possibleCards[i];
                nextCard.button.onClick.RemoveAllListeners();
                nextCard.button.interactable = false;
                nextCard.border.gameObject.SetActive(false);
            }
        }
    }

    void ReceiveChoice(int number)
    {
        //Debug.Log(number);
        choice = number;
    }

#endregion

}
