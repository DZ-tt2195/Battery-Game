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

public class Card : UndoSource
{

#region Variables

    [Foldout("Misc", true)]
        public CardData dataFile;
        [ReadOnly] public Button button;
        [ReadOnly] public int batteries { get; private set; }

    [Foldout("UI", true)]
        [ReadOnly] public CanvasGroup cg;
        public Image background;
        public Image border;
        [SerializeField] TMP_Text batteryDisplay;

    [Foldout("Methods", true)]
        protected bool runNextMethod;
        protected bool runningMethod;

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
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            methodDictionary.Add(methodName, method);
    }

    [PunRPC]
    public void GetActionFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.mainActionData[fileSlot];
        this.transform.SetParent(Manager.instance.actions);
        Manager.instance.listOfActions.Add(this);
        OtherSetup();
    }

    [PunRPC]
    public void GetRobotFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.robotData[fileSlot];
        this.transform.SetParent(Manager.instance.deck);
        OtherSetup();
    }

    void OtherSetup()
    {
        this.name = dataFile.cardName;
        this.gameObject.GetComponent<CardLayout>().FillInCards(this.dataFile, background.color);
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
                    MethodInfo method = typeof(Card).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null && method.ReturnType == typeof(IEnumerator))
                        methodDictionary.Add(methodName, method);
                    else
                        Debug.LogError($"{dataFile.cardName}: instructions: {methodName} doesn't exist");
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
        Log.instance.AddStepRPC(player, null, this, nameof(ChangeBatteries), null, number, false, logged);
        Log.instance.Continue();
    }

    [PunRPC]
    void ChangeBatteries(int logged, bool undo)
    {
        NextStep step = Log.instance.GetCurrentStep();
        if (undo)
        {
            this.batteries -= step.numberToRemember;
        }
        else if (step.numberToRemember != 0)
        {
            int numberToChange = (this.batteries + step.numberToRemember < 0) ? this.batteries : this.batteries + step.numberToRemember;
            this.batteries += numberToChange;
            Log.instance.MultiFunction(nameof(Log.instance.ChangeNumber), RpcTarget.All, new object[1] { numberToChange });

            if (numberToChange > 0)
                Log.instance.AddText($"{step.player} adds {numberToChange} Battery to {this.name}.", logged);
            else
                Log.instance.AddText($"{step.player} removes {numberToChange} Battery from {this.name}.", logged);
        }
        UpdateBatteryText();
    }

    void UpdateBatteryText()
    {
        batteryDisplay.text = KeywordTooltip.instance.EditText($"{batteries} Battery");
        batteryDisplay.transform.parent.gameObject.SetActive(batteries > 0);
    }

#endregion

#region Follow Instructions

    public IEnumerator PlayInstructions(Player player, int logged)
    {
        yield return ResolveInstructions(dataFile.playInstructions, player, logged, false);
    }

    [PunRPC]
    void StopInstructions()
    {
        runNextMethod = false;
    }

    [PunRPC]
    void FinishedInstructions()
    {
        runningMethod = false;
    }

    IEnumerator ResolveInstructions(string[] listOfInstructions, Player player, int logged, bool undo)
    {
        runNextMethod = true;
        for (int i = 0; i < listOfInstructions.Count(); i++)
        {
            string nextPart = listOfInstructions[i];
            string[] listOfSmallInstructions = DownloadSheets.instance.SpliceString(nextPart, '/');

            if (dataFile.whoToTarget[i] == PlayerTarget.You)
            {
                foreach (string methodName in listOfSmallInstructions)
                    yield return RunStep(methodName, player, logged, undo);
            }
            else
            {
                int playerTracker = player.playerPosition;
                for (int j = 0; j < Manager.instance.playersInOrder.Count; j++)
                {
                    Player nextPlayer = Manager.instance.playersInOrder[playerTracker];
                    runNextMethod = true;

                    if (dataFile.whoToTarget[i] == PlayerTarget.Others && player == nextPlayer)
                        continue;

                    foreach (string methodName in listOfSmallInstructions)
                        yield return RunStep(methodName, nextPlayer, logged, undo);

                    playerTracker = (playerTracker == Manager.instance.playersInOrder.Count - 1) ? 0 : playerTracker + 1;
                }
            }
        }
    }

    IEnumerator RunStep(string methodName, Player player, int logged, bool undo)
    {
        runningMethod = true;
        if (methodName.Equals("None"))
        {

        }
        else if (methodName.Contains("ChooseMethod("))
        {
            string[] choices = methodName.
                Replace("ChooseMethod(", "").
                Replace(")", "").
                Replace("]", "").
                Trim().Split('|');

            Popup popup = Instantiate(CarryVariables.instance.textPopup);
            popup.StatsSetup("Choose an option", Vector3.zero);

            foreach (string next in choices)
            {
                switch (next)
                {
                    case nameof(DrawCards):
                        popup.AddTextButton($"+{dataFile.numCards} Card");
                        break;
                    case nameof(GainCoins):
                        popup.AddTextButton($"+{dataFile.numCoins} Coin");
                        break;
                    default:
                        popup.AddTextButton(next);
                        break;
                }
            }

            yield return popup.WaitForChoice();
            string chosenMethod = choices[popup.chosenButton];
            Destroy(popup.gameObject);
            yield return RunStep(chosenMethod, player, logged, undo);
        }
        else
        {
            StartCoroutine((IEnumerator)methodDictionary[methodName].Invoke(this, new object[3] { player, logged, undo }));

            while (runningMethod)
                yield return null;
            if (!runNextMethod) yield break;
        }
    }

    #endregion

#region Steps

    IEnumerator DrawCards(Player player, int logged, bool undo)
    {
        yield return null;
        player.MultiFunction(nameof(player.RequestDraw), RpcTarget.MasterClient, new object[3] {this.pv.ViewID, dataFile.numCards, logged});
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator GainCoins(Player player, int logged, bool undo)
    {
        yield return null;
        player.CoinRPC(this, dataFile.numCoins, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator LoseCoins(Player player, int logged, bool undo)
    {
        yield return null;
        player.CoinRPC(this, -1*dataFile.numCoins, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator TakeNeg(Player player, int logged, bool undo)
    {
        yield return null;
        player.CrownRPC(this, dataFile.numCrowns, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator RemoveNeg(Player player, int logged, bool undo)
    {
        yield return null;
        player.CrownRPC(this, -1*dataFile.numCrowns, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator DiscardHand(Player player, int logged, bool undo)
    {
        yield return null;
        foreach (Card card in player.listOfHand)
            player.DiscardRPC(this, card, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator MandatoryDiscard(Player player, int logged, bool undo)
    {
        if (player.listOfHand.Count <= dataFile.numCards)
        {
            yield return DiscardHand(player, logged, undo);
        }
        else
        {
            for (int i = 0; i < dataFile.numCards; i++)
            {
                Manager.instance.instructions.text = $"Discard a card ({dataFile.numCards - i} more).";
                player.ChooseCard(this, player.listOfHand, false, logged);
                player.DiscardRPC(this, player.chosenCard, logged);
            }
        }
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator OptionalDiscard(Player player, int logged, bool undo)
    {
        yield return null;
        if (player.listOfHand.Count < dataFile.numCards)
        {
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        }
        else
        {
            for (int i = 0; i < dataFile.numCards; i++)
            {
                Manager.instance.instructions.text = $"Discard a card ({dataFile.numCards - i} more)?";
                player.ChooseCard(this, player.listOfHand, i == 0, logged);

                if (player.chosenCard == null)
                {
                    MultiFunction(nameof(StopInstructions), RpcTarget.All);
                    break;
                }
                else
                {
                    player.DiscardRPC(this, player.chosenCard, logged);
                }
            }
        }
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator ChooseFromPlay(Player player, int logged, bool undo)
    {
        yield return null;
        player.ChooseCard(this, player.listOfPlay, false, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator ChooseFromHand(Player player, int logged, bool undo)
    {
        yield return null;
        player.ChooseCard(this, player.listOfHand, false, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator AddBatteryToOther(Player player, int logged, bool undo)
    {
        yield return null;
        for (int i = 0; i < dataFile.numBatteries; i++)
        {
            Manager.instance.instructions.text = $"Add batteries to your robots ({dataFile.numBatteries-i} more)";
            player.ChooseCard(this, player.listOfPlay.Where(card => card != this).ToList(), false, logged);
            if (player.chosenCard != null)
                player.chosenCard.BatteryRPC(player, 1, logged);
        }
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator MoveBattery(Player player, int logged, bool undo)
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
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator PlayCard(Player player, int logged, bool undo)
    {
        yield return null;
        player.ChooseCardToPlay(this, player.listOfHand.Where(card => card.dataFile.coinCost <= player.coins).ToList(), logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    #endregion

#region Booleans

    IEnumerator HandOrMore(Player player, int logged, bool undo)
    {
        yield return null;
        if (!(player.listOfHand.Count <= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator MoneyOrLess(Player player, int logged, bool undo)
    {
        yield return null;
        if (!(player.coins <= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator MoneyOrMore(Player player, int logged, bool undo)
    {
        yield return null;
        if (!(player.coins >= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator ChosenCard(Player player, int logged, bool undo)
    {
        yield return null;
        if (player.chosenCard == null)
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator YesOrNo(Player player, int logged, bool undo)
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

#region Setters

    [PunRPC]
    void SetAllStats(int number, bool undo)
    {
        float multiplier = (dataFile.numMisc > 0) ? dataFile.numMisc : -1 / dataFile.numMisc;
        dataFile.numCards = (int)Mathf.Floor(number * multiplier);
        dataFile.numCoins = (int)Mathf.Floor(number * multiplier);
        dataFile.numCrowns = (int)Mathf.Floor(number * multiplier);
        dataFile.numBatteries = (int)Mathf.Floor(number * multiplier);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator SetToHandSize(Player player, int logged, bool undo)
    {
        yield return null;
        MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { player.listOfHand.Count });
    }

    IEnumerator SetToNegCrowns(Player player, int logged, bool undo)
    {
        yield return null;
        MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { player.negCrowns });
    }

    IEnumerator SetToCoins(Player player, int logged, bool undo)
    {
        yield return null;
        MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { player.coins });
    }

    #endregion

}
