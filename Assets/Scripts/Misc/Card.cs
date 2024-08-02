using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;
using System.Reflection;
using System.Linq;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using System;
using Photon.Realtime;

public class PlayerMethod
{
    public Player player;
    public string method;

    internal PlayerMethod(Player player, string method)
    {
        this.player = player;
        this.method = method;
    }
}

public class Card : UndoSource
{

#region Variables

    [Foldout("Misc", true)]
        public CardData dataFile;
        [ReadOnly] public Button button;
        [ReadOnly] public int batteries { get; private set; }
        [ReadOnly] public int cardID { get; private set; }

    [Foldout("UI", true)]
        [ReadOnly] public CanvasGroup cg;
        public Image background;
        public Image border;
        [SerializeField] TMP_Text batteryDisplay;

    [Foldout("Methods", true)]
        List<PlayerMethod> listOfMethods = new();
        int methodTracker;
        bool runNextMethod;
        public event Action eventCardDone;
        bool ifElse;
        event Action eventIfElse;

    #endregion

#region Setup

    private void Awake()
    {
        button = GetComponent<Button>();
        pv = GetComponent<PhotonView>();
        cg = transform.Find("Canvas Group").GetComponent<CanvasGroup>();
    }

    protected override void AddToMethodDictionary(string methodName)
    {
        MethodInfo method = typeof(Card).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            Debug.LogError($"{this.name}: {methodName}");
        else if (!methodDictionary.ContainsKey(methodName) && method.ReturnType == typeof(void))
            methodDictionary.Add(methodName, method);
    }

    [PunRPC]
    public void GetActionFile(int fileSlot, int card)
    {
        this.dataFile = DownloadSheets.instance.mainActionData[fileSlot];
        this.transform.SetParent(Manager.instance.actions);
        Manager.instance.listOfActions.Add(this);
        OtherSetup(card);
    }

    [PunRPC]
    public void GetRobotFile(int fileSlot, int card)
    {
        this.dataFile = DownloadSheets.instance.robotData[fileSlot];
        this.transform.SetParent(Manager.instance.deck);
        OtherSetup(card);
    }

    void OtherSetup(int card)
    {
        this.name = dataFile.cardName;
        this.gameObject.GetComponent<CardLayout>().FillInCards(this.dataFile, background.color);
        cardID = card;
        Manager.instance.cardIDs.Insert(card, this);
        GetMethods(dataFile.playInstructions);
    }

    void GetMethods(string[] listOfInstructions)
    {
        foreach (string nextSection in listOfInstructions)
        {
            string[] nextSplit = DownloadSheets.instance.SpliceString(nextSection.Trim(), '/');
            foreach (string methodName in nextSplit)
            {
                if (methodName.Equals("None") || methodName.Equals("") || methodDictionary.ContainsKey(methodName))
                {
                }

                string[] splitIntoChoices = SplitUpChoose(methodName);
                string[] splitIntoIfElse = SplitUpIfElse(methodName);

                if (splitIntoChoices != null)
                {
                    foreach (string next in splitIntoChoices)
                        AddToMethodDictionary(next);
                }
                else if (splitIntoIfElse != null)
                {
                    foreach (string next in splitIntoIfElse)
                        AddToMethodDictionary(next);
                }
                else
                {
                    AddToMethodDictionary(methodName);
                }
            }
        }
    }

    #endregion

#region Animations

    public IEnumerator MoveCard(Vector2 newPos, Vector3 newRot, float waitTime)
    {
        float elapsedTime = 0;
        Vector2 originalPos = this.transform.localPosition;
        Vector3 originalRot = this.transform.localEulerAngles;

        while (elapsedTime < waitTime)
        {
            this.transform.localPosition = Vector2.Lerp(originalPos, newPos, elapsedTime / waitTime);
            this.transform.localEulerAngles = Vector3.Lerp(originalRot, newRot, elapsedTime / waitTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        this.transform.localPosition = newPos;
        this.transform.localEulerAngles = newRot;
    }

    public IEnumerator RevealCard(float totalTime)
    {
        if (cg.alpha == 0)
        {
            transform.localEulerAngles = new Vector3(0, 0, 0);
            float elapsedTime = 0f;

            Vector3 originalRot = this.transform.localEulerAngles;
            Vector3 newRot = new(0, 90, 0);

            while (elapsedTime < totalTime)
            {
                this.transform.localEulerAngles = Vector3.Lerp(originalRot, newRot, elapsedTime / totalTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            cg.alpha = 1;
            elapsedTime = 0f;

            while (elapsedTime < totalTime)
            {
                this.transform.localEulerAngles = Vector3.Lerp(newRot, originalRot, elapsedTime / totalTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            this.transform.localEulerAngles = originalRot;
        }
    }

    private void FixedUpdate()
    {
        try
        {
            this.border.SetAlpha(Manager.instance.opacity);
        }
        catch
        {

        }
    }

    #endregion

#region Batteries

    public void BatteryRPC(Player player, int number, int logged)
    {
        Log.instance.AddStepRPC(1, player, this, nameof(ChangeBatteries), new object[1] { this.batteries + number >= 0 ? number : -this.batteries }, logged);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    [PunRPC]
    void ChangeBatteries(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        try
        {
            int amount = (int)step.infoToRemember[0];
            if (undo)
            {
                this.batteries -= amount;
            }
            else if (amount != 0)
            {
                this.batteries += amount;

                if (amount > 0)
                    Log.instance.AddText($"{step.player.name} adds {Mathf.Abs(amount)} Battery to {this.name}.", logged);
                else
                    Log.instance.AddText($"{step.player.name} removes {Mathf.Abs(amount)} Battery from {this.name}.", logged);
            }
            UpdateBatteryText();
        }
        catch
        {

        }
    }

    void UpdateBatteryText()
    {
        batteryDisplay.text = KeywordTooltip.instance.EditText($"{batteries} Battery");
        batteryDisplay.transform.parent.gameObject.SetActive(batteries > 0);
    }

    #endregion

#region Instructions

    [PunRPC]
    public void AddInstructions(int playerPosition)
    {
        listOfMethods.Clear();
        methodTracker = -1;
        runNextMethod = true;
        Player originalPlayer = Manager.instance.playersInOrder[playerPosition];

        for (int i = 0; i < dataFile.playInstructions.Length; i++)
        {
            string[] listOfSmallInstructions = DownloadSheets.instance.SpliceString(dataFile.playInstructions[i], '/');

            if (dataFile.whoToTarget[i] == PlayerTarget.You)
            {
                foreach (string methodName in listOfSmallInstructions)
                    listOfMethods.Add(new(originalPlayer, methodName));
            }
            else
            {
                int playerTracker = originalPlayer.playerPosition;
                for (int j = 0; j < Manager.instance.playersInOrder.Count; j++)
                {
                    if (!(dataFile.whoToTarget[i] == PlayerTarget.Others && originalPlayer == Manager.instance.playersInOrder[playerTracker]))
                    {
                        foreach (string methodName in listOfSmallInstructions)
                            listOfMethods.Add(new(Manager.instance.playersInOrder[playerTracker], methodName));
                    }
                    playerTracker = (playerTracker + 1) % Manager.instance.playersInOrder.Count;
                }
            }
        }
    }

    [PunRPC]
    public void NextMethod(int logged)
    {
        MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });

        if (runNextMethod && methodTracker < listOfMethods.Count)
        {
            PlayerMethod nextMethod = listOfMethods[methodTracker];
            string[] splitIntoChoices = SplitUpChoose(nextMethod.method);
            string[] splitIntoIfElse = SplitUpIfElse(nextMethod.method);

            if (splitIntoChoices != null)
            {
                ResolveChooseOne(nextMethod.player, splitIntoChoices, logged);
            }
            else if (splitIntoIfElse != null)
            {
                ResolveIfElse(nextMethod.player, splitIntoIfElse, logged);
            }
            else
            {
                Log.instance.AddStepRPC(1, nextMethod.player, this, nextMethod.method, new object[0], logged);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            }
        }
        else
        {
            MultiFunction(nameof(InstructionsComplete), RpcTarget.All);
            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
        }
    }

    public void ResetEvent()
    {
        eventCardDone = null;
        eventIfElse = null;
    }

    [PunRPC]
    void MoveTracker(int adjust)
    {
        methodTracker += adjust;
        runNextMethod = adjust < 0 || methodTracker < listOfMethods.Count;
    }

    [PunRPC]
    void InstructionsComplete()
    {
        runNextMethod = false;
        //Debug.Log($"completed {this.name}'s instructions");
        eventCardDone?.Invoke();
    }

    void ResolveIfElse(Player player, string[] elseIfChain, int logged)
    {
        string boolean = elseIfChain[0];
        Action handler = null;
        handler = () =>
        {
            eventIfElse -= handler;
            if (ifElse)
            {
                Log.instance.AddStepRPC(1, player, this, elseIfChain[1], new object[0], logged);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            }
            else if (elseIfChain.Length == 3)
            {
                Log.instance.AddStepRPC(1, player, this, elseIfChain[2], new object[0], logged);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            }
            else
            {
                Log.instance.AddStepRPC(1, player, this, nameof(DoNothing), new object[0], logged);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            }
        };

        eventIfElse += handler;
        Log.instance.AddStepRPC(1, player, this, elseIfChain[0], new object[0], logged);
        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
    }

    [PunRPC]
    void IfElseCompleted(bool decision)
    {
        ifElse = decision;
        eventIfElse?.Invoke();
    }

    void ResolveChooseOne(Player player, string[] chooseChain, int logged)
    {
        object[] listOfChoices = new object[chooseChain.Length];
        for (int i = 0; i<chooseChain.Length; i++)
        {
            string next = chooseChain[i];
            switch (next)
            {
                case nameof(DrawCards):
                    listOfChoices[i] = $"+{dataFile.numCards} Card";
                    break;
                case nameof(MandatoryDiscard):
                    listOfChoices[i] = $"Discard 1 Card";
                    break;
                case nameof(GainCoins):
                    listOfChoices[i] = $"+{dataFile.numCoins} Coin";
                    break;
                case nameof(LoseCoins):
                    listOfChoices[i] = $"-{dataFile.numCoins} Coin";
                    break;
                case nameof(AddBatteryToMultiple):
                    listOfChoices[i] = $"+{dataFile.numBatteries} Battery";
                    break;
                case nameof(RemoveBatteryFromMultiple):
                    listOfChoices[i] = $"-{dataFile.numBatteries} Battery";
                    break;
                case nameof(TakeNeg):
                    listOfChoices[i] = $"Take -{dataFile.numCrowns} Neg Crown";
                    break;
                case nameof(RemoveNeg):
                    listOfChoices[i] = $"Remove -{dataFile.numCrowns} Neg Crown";
                    break;
                case nameof(PlayCard):
                    listOfChoices[i] = $"Play 1 Card";
                    break;
            }
        }

        Action handler = null;
        handler = () =>
        {
            player.eventChosenCard -= handler;
            Log.instance.AddStepRPC(1, player, this, chooseChain[player.choice], new object[0], logged);
            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
        };

        player.eventChosenCard += handler;
        player.GenericChoose(listOfChoices, false, logged, "Choose an option.");
    }

    string[] SplitUpIfElse(string methodGroup)
    {
        if (methodGroup.Contains("IfElse("))
        {
            string[] choices = methodGroup.Replace("IfElse(", "").Replace(")", "").
            Replace("]", "").Trim().Split('|');
            return choices;
        }
        return null;
    }

    string[] SplitUpChoose(string methodGroup)
    {
        if (methodGroup.Contains("ChooseMethod("))
        {
            string[] choices = methodGroup.Replace("ChooseMethod(", "").Replace(")", "").
            Replace("]", "").Trim().Split('|');
            return choices;
        }
        return null;
    }

    #endregion

#region Steps

    [PunRPC]
    void DoNothing(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void ResolveRobots(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] {-1});
        }
        else if (!undo && step.player.InControl())
        {
            Log.instance.AddStepRPC(1, step.player, step.player, "ChooseNextRobot", new object[0], 0);
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void DrawCards(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            step.player.MultiFunction(nameof(Player.RequestDraw), RpcTarget.MasterClient, new object[2] { dataFile.numCards, logged });
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void GainCoins(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            step.player.CoinRPC(dataFile.numCoins, logged);
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void LoseCoins(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            step.player.CoinRPC(-1 * dataFile.numCoins, logged);
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void TakeNeg(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            step.player.CrownRPC(dataFile.numCrowns, logged);
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void RemoveNeg(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            step.player.CrownRPC(-1*dataFile.numCrowns, logged);
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void MandatoryDiscard(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            int currentCount = (step.infoToRemember.Length == 0) ? 0 : (int)step.infoToRemember[0];

            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                {
                    step.player.DiscardRPC(step.player.chosenCard, logged);
                    currentCount++;

                    if (currentCount == dataFile.numCards)
                    {
                        MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
                    }
                    else
                    {
                        Log.instance.AddStepRPC(1, step.player, this, nameof(MandatoryDiscard), new object[1] { currentCount }, logged);
                        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                    }
                }
                else
                {
                    MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
                }
            };
            step.player.eventChosenCard += handler;
            step.player.GenericChoose(step.player.listOfHand, false, logged, $"Discard a card ({dataFile.numCards-currentCount} more)");
        }
    }

    [PunRPC]
    void AddBatteryToOne(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                    step.player.chosenCard.BatteryRPC(step.player, dataFile.numBatteries, logged);

                MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
            };

            step.player.eventChosenCard += handler;
            step.player.GenericChoose(step.player.listOfPlay, false, logged,
                $"Add {dataFile.numBatteries} Battery to a card.");
        }
    }

    [PunRPC]
    void AddBatteryToMultiple(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            int currentCount = (step.infoToRemember.Length == 0) ? 0 : (int)step.infoToRemember[0];

            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                {
                    step.player.chosenCard.BatteryRPC(step.player, 1, logged);
                    currentCount++;

                    if (currentCount == dataFile.numBatteries)
                    {
                        MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
                    }
                    else
                    {
                        Log.instance.AddStepRPC(1, step.player, this, nameof(AddBatteryToMultiple), new object[1] { currentCount }, logged);
                        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                    }
                }
                else
                {
                    MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
                }
            };
            step.player.eventChosenCard += handler;
            step.player.GenericChoose(step.player.listOfPlay, false, logged,
                $"Add Battery to any card ({dataFile.numBatteries - currentCount} more)");
        }
    }

    [PunRPC]
    void AddBatteryToChosen(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            if (step.player.chosenCard != null)
                step.player.chosenCard.BatteryRPC(step.player, dataFile.numBatteries, logged);
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void RemoveBatteryFromOne(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                    step.player.chosenCard.BatteryRPC(step.player, -1 * dataFile.numBatteries, logged);

                MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
            };

            step.player.eventChosenCard += handler;
            step.player.GenericChoose(step.player.listOfPlay.Where(card => card.batteries>=dataFile.numBatteries).ToList(),
                false, logged, $"Remove {dataFile.numBatteries} Battery from a card.");
        }
    }

    [PunRPC]
    void RemoveBatteryFromMultiple(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            int currentCount = (step.infoToRemember.Length == 0) ? 0 : (int)step.infoToRemember[0];

            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                {
                    step.player.chosenCard.BatteryRPC(step.player, -1, logged);
                    currentCount++;

                    if (currentCount == dataFile.numBatteries)
                    {
                        MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
                    }
                    else
                    {
                        Log.instance.AddStepRPC(1, step.player, this, nameof(RemoveBatteryFromMultiple), new object[1] { currentCount }, logged);
                        Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                    }
                }
                else
                {
                    MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
                }
            };
            step.player.eventChosenCard += handler;
            step.player.GenericChoose(step.player.listOfPlay.Where(card => card.batteries >= 1).ToList(), false, logged,
                $"Remove Battery from any card ({dataFile.numBatteries - currentCount} more)");
        }
    }

    [PunRPC]
    void RemoveBatteryFromChosen(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            if (step.player.chosenCard != null)
                step.player.chosenCard.BatteryRPC(step.player, -1 * dataFile.numBatteries, logged);
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void PlayCard(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                {
                    Log.instance.AddStepRPC(1, step.player, step.player, nameof(Player.PlayFromHand),
                        new object[1] { step.player.chosenCard.cardID }, logged);
                    Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                }
                MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
            };
            step.player.eventChosenCard += handler;
            step.player.GenericChoose(step.player.listOfHand.Where
                (card => card.dataFile.coinCost <= step.player.coins).ToList(),
                true, logged, $"Play a card?");
        }
    }

    #endregion

#region Booleans

    [PunRPC]
    void HandOrMore(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.listOfHand.Count >= dataFile.numMisc });
        }
    }

    [PunRPC]
    void HandOrLess(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.listOfHand.Count <= dataFile.numMisc });
        }
    }

    [PunRPC]
    void PlayAreaOrMore(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.listOfPlay.Count >= dataFile.numMisc });
        }
    }

    [PunRPC]
    void PlayAreaOrLess(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.listOfPlay.Count <= dataFile.numMisc });
        }
    }

    [PunRPC]
    void MoneyOrMore(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.coins >= dataFile.numMisc });
        }
    }

    [PunRPC]
    void MoneyOrLess(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.coins <= dataFile.numMisc });
        }
    }

    [PunRPC]
    void NegCrownOrMore(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.negCrowns >= dataFile.numMisc });
        }
    }

    [PunRPC]
    void NegCrownOrLess(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.negCrowns <= dataFile.numMisc });
    }

    [PunRPC]
    void ThisBatteryOrMore(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { this.batteries >= dataFile.numMisc });
        }
    }

    [PunRPC]
    void ThisBatteryOrLess(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { this.batteries <= dataFile.numMisc });
        }
    }

    [PunRPC]
    void TotalBatteryOrMore(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.TotalBatteries() >= dataFile.numMisc });
        }
    }

    [PunRPC]
    void TotalBatteryOrLess(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.TotalBatteries() <= dataFile.numMisc });
        }
    }

    [PunRPC]
    void YesOrNo(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                    new object[1] { step.player.choice == 0 });
            };

            step.player.eventChosenCard += handler;
            step.player.GenericChoose(new object[2] {"Yes", "No"}, false, logged, $"Resolve {this.name}?");
        }
    }

    [PunRPC]
    void OptionalDiscard(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            int currentCount = (step.infoToRemember.Length == 0) ? 0 : (int)step.infoToRemember[0];

            if (step.player.listOfHand.Count < dataFile.numCards)
            {
                MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { false });
            }
            else
            {
                Action handler = null;
                handler = () =>
                {
                    step.player.eventChosenCard -= handler;
                    if (step.player.chosenCard != null)
                    {
                        currentCount++;
                        step.player.DiscardRPC(step.player.chosenCard, logged);

                        if (currentCount == dataFile.numCards)
                        {
                            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                            new object[1] { true });
                        }
                        else
                        {
                            Log.instance.AddStepRPC(1, step.player, this, nameof(OptionalDiscard), new object[1] { currentCount }, logged);
                            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                        }
                    }
                    else
                    {
                        MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                        new object[1] { false });
                    }
                };
                step.player.eventChosenCard += handler;
                step.player.GenericChoose(step.player.listOfHand, currentCount == 0, logged,
                    $"Discard a Card? ({dataFile.numCards - currentCount} more)");
            }
        }
    }

    [PunRPC]
    void OptionalTakeNeg(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.choice == 0)
                    step.player.CrownRPC(dataFile.numCrowns, logged);

                MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.choice == 0 });
            };
            step.player.eventChosenCard += handler;
            step.player.GenericChoose(new object[2] {"Yes", "No"}, true, logged,
                $"Take -{dataFile.numCrowns} Neg Crown?");
        }
    }

    [PunRPC]
    void OptionalLoseCoins(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            if (step.player.coins < dataFile.numCoins)
            {
                MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { false });
            }
            else
            {
                Action handler = null;
                handler = () =>
                {
                    step.player.eventChosenCard -= handler;
                    if (step.player.choice == 0)
                        step.player.CoinRPC(-1 * dataFile.numCoins, logged);

                    MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                    new object[1] { step.player.choice == 0 });
                };
                step.player.eventChosenCard += handler;
                step.player.GenericChoose(new object[2] { "Yes", "No" }, true, logged,
                    $"Pay {dataFile.numCoins} Coin?");
            }
        }
    }

    [PunRPC]
    void OptionalRemoveBatteryFromOne(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                    step.player.chosenCard.BatteryRPC(step.player, -1 * dataFile.numBatteries, logged);

                MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { step.player.choice >= 0 });
            };
            step.player.eventChosenCard += handler;
            step.player.GenericChoose(step.player.listOfPlay.Where
                (card => card.batteries >= dataFile.numBatteries).ToList(), true, logged,
                $"Remove {dataFile.numBatteries} Battery from a card?");
        }
    }

    [PunRPC]
    void OptionalRemoveBatteryFromMultiple(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            int currentCount = (step.infoToRemember.Length == 0) ? 0 : (int)step.infoToRemember[0];

            if (step.player.listOfHand.Count < dataFile.numCards)
            {
                MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                new object[1] { false });
            }
            else
            {
                Action handler = null;
                handler = () =>
                {
                    step.player.eventChosenCard -= handler;
                    if (step.player.chosenCard != null)
                    {
                        currentCount++;
                        step.player.chosenCard.BatteryRPC(step.player, -1, logged);

                        if (currentCount == dataFile.numBatteries)
                        {
                            MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                            new object[1] { true });
                        }
                        else
                        {
                            Log.instance.AddStepRPC(1, step.player, this, nameof(OptionalRemoveBatteryFromMultiple), new object[1] { currentCount }, logged);
                            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
                        }
                    }
                    else
                    {
                        MultiFunction(nameof(IfElseCompleted), RpcTarget.MasterClient,
                        new object[1] { false });
                    }
                };
                step.player.eventChosenCard += handler;
                step.player.GenericChoose(step.player.listOfPlay.Where(card => card.batteries>=1).ToList(), currentCount == 0, logged,
                    $"Remove a Battery? ({dataFile.numBatteries - currentCount} more)");
            }
        }
    }

    #endregion

#region Setters

    [PunRPC]
    void SetAllStats(int number)
    {
        float multiplier = (dataFile.numMisc > 0) ? dataFile.numMisc : -1 / dataFile.numMisc;
        dataFile.numCards = (int)Mathf.Floor(number * multiplier);
        dataFile.numCoins = (int)Mathf.Floor(number * multiplier);
        dataFile.numCrowns = (int)Mathf.Floor(number * multiplier);
        dataFile.numBatteries = (int)Mathf.Floor(number * multiplier);
    }

    [PunRPC]
    void SetToHandSize(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { step.player.listOfHand.Count });
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void SetToNegCrowns(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { step.player.negCrowns });
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void SetToCoins(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { step.player.coins });
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void SetToPlayArea(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { step.player.listOfPlay.Count });
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void SetToTotalBatteries(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            int totalBatteries = 0;
            foreach (Card card in step.player.listOfPlay)
                totalBatteries += card.batteries;

            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { totalBatteries });
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    [PunRPC]
    void SetToBatteriesHere(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { this.batteries });
            MultiFunction(nameof(NextMethod), RpcTarget.MasterClient, new object[1] { logged });
        }
    }

    #endregion

}
