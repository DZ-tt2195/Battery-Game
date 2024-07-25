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
using System;

public class Player : UndoSource
{

#region Variables

    [Foldout("Prefabs", true)]
        [SerializeField] Button playerButtonPrefab;

    [Foldout("Player info", true)]
        Canvas canvas;
        [ReadOnly] public int coins;
        [ReadOnly] public int negCrowns;
        [ReadOnly] public int playerPosition;
        [ReadOnly] public Photon.Realtime.Player realTimePlayer { get; private set; }

    [Foldout("UI", true)]
        [ReadOnly] public List<Card> listOfHand = new List<Card>();
        [SerializeField] Transform cardhand;
        [ReadOnly] public List<Card> listOfPlay = new List<Card>();
        [SerializeField] Transform cardplay;
        TMP_Text buttonText;
        Transform storePlayers;
        Button resignButton;

    [Foldout("Choices", true)]
        List<Card> resolvedCards = new();
        public event Action eventChosenCard = null;
        [ReadOnly] public Card chosenCard;
        [ReadOnly] public int choice { get; private set; }

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

    private void Start()
    {
        if (PhotonNetwork.IsConnected)
        {
            this.name = pv.Owner.NickName;
            //Debug.Log($"{pv.Owner.NickName}: {pv.OwnerActorNr}");
        }
    }

    protected override void AddToMethodDictionary(string methodName)
    {
        MethodInfo method = typeof(Player).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        try
        {
            if (method != null && method.ReturnType == typeof(void))
                methodDictionary.Add(methodName, method);
        }
        catch
        {
            Debug.LogError(methodName);
        }
    }

    internal void AssignInfo(int position)
    {
        this.playerPosition = position;
        storePlayers = GameObject.Find("Store Players").transform;
        this.transform.SetParent(storePlayers);
        this.transform.localPosition = new Vector3(2500 * this.playerPosition, 0, 0);

        Button newButton = Instantiate(playerButtonPrefab, Vector3.zero, new Quaternion());
        newButton.transform.SetParent(this.transform.parent.parent);
        newButton.transform.localPosition = new(-1100, 400 - (200 * playerPosition));
        buttonText = newButton.transform.GetChild(0).GetComponent<TMP_Text>();
        newButton.onClick.AddListener(MoveScreen);

        if (PhotonNetwork.IsConnected)
            realTimePlayer = PhotonNetwork.PlayerList[pv.OwnerActorNr-1];

        if (InControl())
        {
            resignButton.onClick.AddListener(() =>
            Manager.instance.MultiFunction(nameof(Manager.instance.DisplayEnding),
            RpcTarget.All, new object[1] { this.playerPosition }));
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

    public bool InControl()
    {
        if (PhotonNetwork.IsConnected)
            return this.pv.AmOwner;
        else
            return true;
    }

    #endregion

#region Cards in Hand

    public void DiscardRPC(Card card, int logged)
    {
        Log.instance.AddStepRPC(1, this, this, nameof(DiscardFromHand), new object[1] { card.cardID }, logged);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    [PunRPC]
    void DiscardFromHand(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            try
            {
                StartInHand(Manager.instance.cardIDs[(int)step.infoToRemember[0]], 0f);
            }
            catch
            {

            }
        }
        else
        {
            Card discardMe = Manager.instance.cardIDs[(int)step.infoToRemember[0]];
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
        object[] listOfCardIDs = new object[cardsToDraw];

        for (int i = 0; i < cardsToDraw; i++)
        {
            if (Manager.instance.deck.childCount == 0)
            {
                Manager.instance.discard.Shuffle();
                while (Manager.instance.discard.childCount > 0)
                    Manager.instance.discard.GetChild(0).SetParent(Manager.instance.deck);
            }

            listOfCardIDs[i] = Manager.instance.deck.GetChild(i).GetComponent<Card>().cardID;
        }

        Log.instance.AddStepRPC(1, this, this, nameof(DrawFromDeck), listOfCardIDs, logged);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    [PunRPC]
    void DrawFromDeck(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            foreach (object next in step.infoToRemember.AsEnumerable().Reverse())
            {
                try
                {
                    Card card = Manager.instance.cardIDs[(int)next];
                    card.transform.SetParent(Manager.instance.deck);
                    card.transform.localPosition = new(-10000, -10000);
                    card.transform.SetAsFirstSibling();
                    listOfHand.Remove(card);
                }
                catch
                {
                }
            }
        }
        else
        {
            string cardList = "";
            for (int i = 0; i < step.infoToRemember.Length; i++)
            {
                Card newCard = Manager.instance.cardIDs[(int)step.infoToRemember[i]];
                StartInHand(newCard, 0.3f);
                cardList += $"{newCard.name}{(i < step.infoToRemember.Length - 1 ? ", " : ".")}";
            }
            Log.instance.AddText($"{this.name} draws {step.infoToRemember.Length} Card.", logged);
        }
        SortHand();
    }

    void StartInHand(Card newCard, float time)
    {
        newCard.transform.SetParent(this.cardhand);
        newCard.transform.localPosition = new Vector2(0, -1100);
        newCard.cg.alpha = 0;
        listOfHand.Add(newCard);

        if (InControl())
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
            StartCoroutine(nextCard.MoveCard(newPosition, nextCard.transform.localEulerAngles, 0.25f));
        }

        if (InControl())
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

    [PunRPC]
    public void PlayFromHand(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (step.infoToRemember.Length != 0)
        {
            Card newCard = Manager.instance.cardIDs[(int)step.infoToRemember[0]];
            if (undo)
            {
                listOfPlay.Remove(newCard);
                StartInHand(newCard, 0f);
            }
            else
            {
                newCard.name = newCard.name.Replace("(Clone)", "");
                newCard.cg.alpha = 1;

                listOfHand.Remove(newCard);
                listOfPlay.Add(newCard);
                newCard.transform.SetParent(cardplay);

                Log.instance.AddText($"{this.name} plays {newCard.name}.", logged);
                if (InControl())
                    CoinRPC(-1 * newCard.dataFile.coinCost, logged);
                newCard.BatteryRPC(this, newCard.dataFile.startingBatteries, logged);
            }
            SortHand();
            SortPlay();
        }
    }

    #endregion

#region Resources

    public void CoinRPC(int number, int logged)
    {
        Log.instance.AddStepRPC(1, this, this, nameof(ChangeCoin), new object[1] { this.coins + number >= 0 ? number : -this.coins }, logged);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    [PunRPC]
    void ChangeCoin(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();

        try
        {
            int amount = (int)step.infoToRemember[0];

            if (amount != 0)
            {
                if (undo)
                {
                    this.coins -= amount;
                }
                else
                {
                    this.coins += amount;

                    if (amount > 0)
                        Log.instance.AddText($"{this.name} gains {Mathf.Abs(amount)} Coin.", logged);
                    else
                        Log.instance.AddText($"{this.name} loses {Mathf.Abs(amount)} Coin.", logged);
                }
                UpdateButton();
            }
        }
        catch
        {
        }
    }

    public void CrownRPC(int number, int logged)
    {
        Log.instance.AddStepRPC(1, this, this, nameof(ChangeCrown), new object[1] { this.negCrowns + number >= 0 ? number : -this.negCrowns }, logged);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    [PunRPC]
    void ChangeCrown(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        try
        {
            int amount = (int)step.infoToRemember[0];

            if (amount != 0)
            {
                if (undo)
                {
                    this.negCrowns -= amount;
                }
                else
                {
                    this.negCrowns += amount;

                    if (amount > 0)
                        Log.instance.AddText($"{this.name} takes -{Mathf.Abs(amount)} Neg Crown.", logged);
                    else
                        Log.instance.AddText($"{this.name} removes -{Mathf.Abs(amount)} Neg Crown.", logged);
                }
                UpdateButton();
            }
        }
        catch
        {
        }
    }

    public int CalculateScore()
    {
        int score = negCrowns;
        foreach (Card card in listOfPlay)
            score += card.dataFile.scoringCrowns;
        return score;
    }

    #endregion

#region Turn

    [PunRPC]
    public void StartTurn(int logged, bool undo)
    {
        if (InControl() && !undo)
        {
            resolvedCards.Clear();

            Action handler = null;
            handler = () =>
            {
                eventChosenCard -= handler;
                ResolveCardInstructions(chosenCard, logged);
            };
            eventChosenCard += handler;

            Log.instance.AddStepRPC(1, this, this, nameof(ChooseCardFromPopup),
                ConvertCardList(Manager.instance.listOfActions, new object[1] { false }), 0);
            Log.instance.MultiFunction(nameof(Log.instance.CurrentStepNeedsDecision), RpcTarget.All, new object[1] { this.playerPosition });
            Manager.instance.InstructionsRPC(this.playerPosition, "Choose an Action.");
            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
        }
    }

    void ResolveCardInstructions(Card card, int logged)
    {
        if (card != null)
        {
            Log.instance.MultiFunction(nameof(Log.instance.AddText), RpcTarget.All, new object[2] { $"{this.name} resolves {card.name}.", logged });
            card.MultiFunction(nameof(card.AddInstructions), RpcTarget.All, new object[1] {this.playerPosition});
            card.MultiFunction(nameof(Card.NextMethod), RpcTarget.All, new object[1] { logged+1 });
            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
        }
    }

    [PunRPC]
    void ChooseNextRobot(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && InControl())
        {
            List<Card> availableOptions = listOfPlay.Where(card => card.batteries > 0).ToList();
            foreach (Card card in resolvedCards)
                availableOptions.Remove(card);

            Action handler = null;
            handler = () =>
            {
                eventChosenCard -= handler;
                Log.instance.AddStepRPC(1, this, this, nameof(ResolveNextRobot), new object[1] {chosenCard == null ? null : chosenCard.cardID }, 0);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            };
            eventChosenCard += handler;

            if (availableOptions.Count >= 2)
                Log.instance.MultiFunction(nameof(Log.instance.CurrentStepNeedsDecision), RpcTarget.All, new object[1] { this.playerPosition });

            Log.instance.AddStepRPC(1, this, this, nameof(ChooseCardOnScreen),
                ConvertCardList(availableOptions, new object[1] { false }), 0);
            Manager.instance.InstructionsRPC(this.playerPosition, "Choose your next Robot.");
            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
        }
    }

    [PunRPC]
    void ResolveNextRobot(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        try
        {
            Card nextRobot = Manager.instance.cardIDs[(int)step.infoToRemember[0]];

            if (undo)
            {
                resolvedCards.Remove(nextRobot);
            }
            else
            {
                resolvedCards.Add(nextRobot);
                nextRobot.BatteryRPC(this, -1, logged);

                Action handler = null;
                handler = () =>
                {
                    nextRobot.eventCardDone -= handler;
                    Log.instance.AddStepRPC(1, this, this, nameof(ChooseNextRobot), new object[0], 0);
                    Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                };
                nextRobot.eventCardDone += handler;
                ResolveCardInstructions(nextRobot, logged);
            }
        }
        catch
        {
            Player nextPlayer = Manager.instance.playersInOrder[(playerPosition + 1) % Manager.instance.playersInOrder.Count];
            if (nextPlayer.playerPosition == 0)
            {
                if (undo)
                {
                    Manager.instance.MultiFunction(nameof(Manager.instance.ChangeTurnNumber),
                        RpcTarget.All, new object[1] { Manager.instance.turnNumber - 1 });
                }
                else if (Manager.instance.PlayerWon())
                {
                    Manager.instance.MultiFunction(nameof(Manager.instance.DisplayEnding), RpcTarget.All, new object[1] { -1 });
                }
                else
                {
                    Manager.instance.PrintPlayerTurn(nextPlayer, Manager.instance.turnNumber + 1);
                    Log.instance.AddStepRPC(1, nextPlayer, nextPlayer, nameof(StartTurn), new object[0], 0);
                    Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                }
            }
            else if (!undo)
            {
                Manager.instance.PrintPlayerTurn(nextPlayer, Manager.instance.turnNumber);
                Log.instance.AddStepRPC(1, nextPlayer, nextPlayer, nameof(StartTurn), new object[0], 0);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            }
        }
    }

    #endregion

#region Decisions

    public void GenericChooseCard(List<Card> possibleCards, bool optional, int logged, string changeInstructions)
    {
        Log.instance.AddStepRPC(1, this, this, nameof(ChooseCardOnScreen),
            ConvertCardList(possibleCards, new object[1] { optional }), logged);
        Manager.instance.InstructionsRPC(this.playerPosition, changeInstructions);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    public void GenericChooseOption(object[] possibleChoices, bool optional, int logged, string changeInstructions)
    {
        object[] array = new object[1 + possibleChoices.Length];
        array[0] = optional;
        for (int i = 0; i<possibleChoices.Length; i++)
            array[i + 1] = possibleChoices[i];

        Log.instance.AddStepRPC(1, this, this, nameof(ChooseOptionFromPopup), array, logged);
        Manager.instance.InstructionsRPC(this.playerPosition, changeInstructions);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    object[] ConvertCardList(List<Card> possibleChoices, object[] startingParameters)
    {
        object[] objectList = new object[startingParameters.Length + possibleChoices.Count];
        int currentCounter = 0;

        for (int i = 0; i < startingParameters.Length; i++)
        {
            objectList[i] = startingParameters[i];
            currentCounter++;
        }

        for (int i = 0; i < possibleChoices.Count; i++)
        {
            objectList[currentCounter] = possibleChoices[i].cardID;
            currentCounter++;
        }
        return objectList;
    }

    public void ResetEvent()
    {
        eventChosenCard = null;
    }

    public void MadeDecision(Card card, int value)
    {
        //Debug.Log($"{(card == null ? "null" : card.name)}, {(value == CarryVariables.instance.undecided ? "undecided" : value)}");
        chosenCard = card;
        choice = value;
        if (value != CarryVariables.instance.undecided)
            eventChosenCard?.Invoke();
    }

    [PunRPC]
    void ChooseCardOnScreen(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && InControl())
        {
            MadeDecision(null, CarryVariables.instance.undecided);
            bool optional = (bool)step.infoToRemember[0];
            Popup popup = null;
            IEnumerator haveCardsEnabled = KeepCardsOn(step.infoToRemember);

            if (optional)
            {
                popup = Instantiate(CarryVariables.instance.textPopup);
                popup.transform.SetParent(GameObject.Find("Canvas").transform);
                popup.StatsSetup(this, "Decline?", Vector3.zero);
                popup.AddTextButton("Decline");
                popup.WaitForChoice();
            }

            Action handler = null;
            handler = () =>
            {
                eventChosenCard -= handler;
                StopCoroutine(haveCardsEnabled);
                for (int i = 1; i < step.infoToRemember.Length; i++)
                {
                    Card nextCard = Manager.instance.cardIDs[(int)step.infoToRemember[i]];
                    nextCard.button.onClick.RemoveAllListeners();
                    nextCard.button.interactable = false;
                    nextCard.border.gameObject.SetActive(false);
                }

                if (popup != null)
                    Destroy(popup.gameObject);
            };
            eventChosenCard += handler;

            if (step.infoToRemember.Length == 1 && !optional)
                MadeDecision(null, -1);
            else if (step.infoToRemember.Length == 2 && !optional)
                MadeDecision(Manager.instance.cardIDs[(int)step.infoToRemember[1]], 1);
            else
                StartCoroutine(haveCardsEnabled);
        }
    }

    IEnumerator KeepCardsOn(object[] infoToRemember)
    {
        float elapsedTime = 0f;
        while (elapsedTime < 1.5f)
        {
            for (int j = 1; j < infoToRemember.Length; j++)
            {
                Card nextCard = Manager.instance.cardIDs[(int)infoToRemember[j]];
                int buttonNumber = j;

                nextCard.button.onClick.RemoveAllListeners();
                nextCard.button.interactable = true;
                nextCard.button.onClick.AddListener(() => MadeDecision(nextCard, buttonNumber));
                nextCard.border.gameObject.SetActive(true);
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    [PunRPC]
    void ChooseCardFromPopup(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && InControl())
        {
            Popup popup = Instantiate(CarryVariables.instance.cardPopup);
            popup.transform.SetParent(this.transform);
            popup.StatsSetup(this, "Choices", Vector3.zero);

            for (int i = 1; i < step.infoToRemember.Length; i++)
            {
                Card card = Manager.instance.cardIDs[(int)step.infoToRemember[i]];
                popup.AddCardButton(card, 1);
            }

            Action handler = null;
            handler = () =>
            {
                eventChosenCard -= handler;
                Destroy(popup.gameObject);
            };
            eventChosenCard += handler;
            popup.WaitForChoice();
        }
    }

    [PunRPC]
    void ChooseOptionFromPopup(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && InControl())
        {
            Popup popup = Instantiate(CarryVariables.instance.textPopup);
            popup.transform.SetParent(this.transform);
            popup.StatsSetup(this, "Choices", Vector3.zero);

            for (int i = 1; i < step.infoToRemember.Length; i++)
            {
                popup.AddTextButton((string)step.infoToRemember[i]);
            }

            Action handler = null;
            handler = () =>
            {
                eventChosenCard -= handler;
                Destroy(popup.gameObject);
            };
            eventChosenCard += handler;
            popup.WaitForChoice();
        }
    }

    #endregion

}
