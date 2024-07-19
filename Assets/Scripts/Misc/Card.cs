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
        Player originalPlayer;
        public event Action eventCompletedCard;
        public bool runNextMethod { get; private set; }

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
        try
        {
            if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
                methodDictionary.Add(methodName, method);
        }
        catch
        {
            Debug.LogError(methodName);
        }
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

                else if (methodName.Contains("ChooseMethod("))
                {
                    string[] choices = methodName.
                        Replace("ChooseMethod(", "").
                        Replace(")", "").
                        Replace("]", "").
                        Trim().Split('|');
                    GetMethods(choices);
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
        methodTracker = 0;
        runNextMethod = true;
        originalPlayer = Manager.instance.playersInOrder[playerPosition];

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
        /*
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            Log.instance.AddStepRPC(1, originalPlayer, null, this,
                nameof(NextStep), new object[0], logged);
            Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
        }
        */
    }

    [PunRPC]
    void NextMethod(int logged, bool undo)
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            if (undo)
            {
            }
            else if (runNextMethod && methodTracker < listOfMethods.Count)
            {
                PlayerMethod nextMethod = listOfMethods[methodTracker];

                Log.instance.AddStepRPC(1, nextMethod.player, this,
                    nextMethod.method, new object[0], logged);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            }
            else
            {
                methodTracker = listOfMethods.Count;
                MultiFunction(nameof(StopInstructions), RpcTarget.All);
                Log.instance.MultiFunction(nameof(Log.instance.Continue), RpcTarget.All);
            }
        }
    }

    [PunRPC]
    void MoveTracker(int adjust)
    {
        methodTracker += adjust;
        if (adjust < 0)
            runNextMethod = false;
    }

    [PunRPC]
    void StopInstructions()
    {
        runNextMethod = false;
        //Debug.Log($"completed {this.name}'s instructions");
        eventCompletedCard?.Invoke();
    }

    public void ResetEvent()
    {
        eventCompletedCard = null;
    }

    #endregion

#region Steps

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
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
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
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
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
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
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
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
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
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
        }
    }

    [PunRPC]
    void RemoveNeg(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (!undo && step.player.InControl())
        {
            step.player.CrownRPC(-1 * dataFile.numCrowns, logged);
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
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
            Action handler = null;
            handler = () =>
            {
                step.player.eventChosenCard -= handler;
                if (step.player.chosenCard != null)
                    step.player.DiscardRPC(step.player.chosenCard, logged);

                MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
                MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
            };
            step.player.eventChosenCard += handler;
            List<Card> canDiscard = step.player.listOfHand;
            step.player.ChooseCard(canDiscard, false, logged, $"Discard a card.");
        }
    }

    [PunRPC]
    void AddBatteryToOther(int logged, bool undo)
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

                MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
                MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
            };

            step.player.eventChosenCard += handler;
            List<Card> otherCards = step.player.listOfPlay;
            otherCards.Remove(this);
            step.player.ChooseCard(otherCards, false, logged, $"Add {dataFile.numBatteries} Battery to a card.");
        }
    }

    [PunRPC]
    void RemoveBatteryFromOther(int logged, bool undo)
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

                MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
                MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
            };

            step.player.eventChosenCard += handler;
            List<Card> otherCards = step.player.listOfPlay;
            otherCards.Remove(this);
            step.player.ChooseCard(otherCards, false, logged, $"Add {dataFile.numBatteries} Battery to a card.");
        }
    }

    [PunRPC]
    void AddBatteryToSelf(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { -1 });
        }
        else if (!undo && step.player.InControl())
        {
            this.BatteryRPC(step.player, dataFile.numBatteries, logged);
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
        }
    }

    /*
    [PunRPC]
    void MoveBattery( int logged, bool undo)
    {
        yield return null;
        if (player.listOfPlay.Count >= 2)
        {
            Manager.instance.instructions.text = $"Remove 1 battery from a robot in play.";
            player.ChooseCard(this, player.listOfPlay.Where(card => card.batteries > 0).ToList(), false, logged);
            Card firstCard = player.chosenCard;
            if (firstCard != null)
            {
                firstCard.BatteryRPC(player, -1, logged);

                Manager.instance.instructions.text = $"Add 1 battery to a robot in play.";
                player.ChooseCard(this, player.listOfPlay.Where(card => card != this).ToList(), false, logged);
                if (player.chosenCard != null)
                    player.chosenCard.BatteryRPC(player, 1, logged);
            }
        }
        //MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }
    */

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
                MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
                MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
            };
            step.player.eventChosenCard += handler;
            List<Card> possibleCards = step.player.listOfHand.Where(card => card.dataFile.coinCost <= step.player.coins).ToList();
            step.player.ChooseCard(possibleCards, false, logged, $"Play a card?");
        }
    }

    #endregion

    /*
#region Booleans

    void HandOrMore(Player player, int logged, bool undo)
    {
        yield return null;
        if (!(player.listOfHand.Count <= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    void MoneyOrLess(Player player, int logged, bool undo)
    {
        yield return null;
        if (!(player.coins <= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    void MoneyOrMore(Player player, int logged, bool undo)
    {
        yield return null;
        if (!(player.coins >= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    void YesOrNo(Player player, int logged, bool undo)
    {
        Popup popup = Instantiate(CarryVariables.instance.textPopup);
        popup.transform.SetParent(GameObject.Find("Canvas").transform);
        popup.StatsSetup(this.name, Vector3.zero);
        popup.AddTextButton("Yes");
        popup.AddTextButton("No");

        yield return popup.WaitForChoice();
        if (popup.chosenButton == 1)
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    #endregion
    */

#region Setters

    [PunRPC]
    void SetAllStats(int number, bool undo)
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
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { step.player.listOfHand.Count });
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
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
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { step.player.negCrowns });
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
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
        if (!undo && step.player.InControl())
        {
            MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { step.player.coins });
            MultiFunction(nameof(MoveTracker), RpcTarget.All, new object[1] { 1 });
            MultiFunction(nameof(NextMethod), RpcTarget.All, new object[2] { logged, undo });
        }
    }

#endregion

}
