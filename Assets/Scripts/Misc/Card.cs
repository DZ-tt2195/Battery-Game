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

[RequireComponent(typeof(PhotonView))]
public class Card : MonoBehaviour
{

#region Variables

    [Foldout("Misc", true)]
        [ReadOnly] public PhotonView pv;
        public CardData dataFile;
        [ReadOnly] public Button button;

    [Foldout("Art", true)]
        [ReadOnly] public CanvasGroup cg;
        public Image background;
        [ReadOnly] public Sprite originalSprite;
        public Image border;

    [Foldout("Methods", true)]
        public Dictionary<string, MethodInfo> dictionary = new();
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

    void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            dictionary[methodName].Invoke(this, parameters);
    }

    IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            yield return (IEnumerator)dictionary[methodName].Invoke(this, parameters);
    }

    void AddToDictionary(string methodName)
    {
        MethodInfo method = typeof(Card).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    [PunRPC]
    public void GetActionFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.mainActionData[fileSlot];
        this.transform.SetParent(Manager.instance.actions);
        Manager.instance.listOfActions.Add(this);
        this.originalSprite = Resources.Load<Sprite>($"Action/{this.dataFile.cardName}");
        OtherSetup();
    }

    [PunRPC]
    public void GetRobotFile(int fileSlot)
    {
        this.dataFile = DownloadSheets.instance.robotData[fileSlot];
        this.transform.SetParent(Manager.instance.deck);
        this.originalSprite = Resources.Load<Sprite>($"Robot/{this.dataFile.cardName}");
        OtherSetup();
    }

    void OtherSetup()
    {
        this.name = dataFile.cardName;
        this.gameObject.GetComponent<CardLayout>().FillInCards(this.dataFile, this.originalSprite, background.color);

        GetMethods(dataFile.playInstructions);
    }

    void GetMethods(string[] listOfInstructions)
    {
        foreach (string nextSection in listOfInstructions)
        {
            string[] nextSplit = DownloadSheets.instance.SpliceString(nextSection.Trim(), '/');
            foreach (string methodName in nextSplit)
            {
                if (methodName.Equals("None") || methodName.Equals("") || dictionary.ContainsKey(methodName))
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
                        dictionary.Add(methodName, method);
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
        this.border.SetAlpha(Manager.instance.opacity);
    }

    #endregion

#region Follow Instructions

    public IEnumerator PlayInstructions(Player player, int logged)
    {
        if (player.ignoreInstructions == 0 || Manager.instance.listOfActions.Contains(this))
            yield return ResolveInstructions(dataFile.playInstructions, player, logged);
        else
            Log.instance.MultiFunction(nameof(Log.instance.AddText), RpcTarget.All, new object[2] { $"{this.name} ignores {this.name}'s instructions.", logged });
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

    IEnumerator ResolveInstructions(string[] listOfInstructions, Player player, int logged)
    {
        runNextMethod = true;
        for (int i = 0; i < listOfInstructions.Count(); i++)
        {
            string nextPart = listOfInstructions[i];
            string[] listOfSmallInstructions = DownloadSheets.instance.SpliceString(nextPart, '/');

            if (dataFile.whoToTarget[i] == PlayerTarget.You)
            {
                foreach (string methodName in listOfSmallInstructions)
                    yield return RunStep(methodName, player, logged);
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
                        yield return RunStep(methodName, nextPlayer, logged);

                    playerTracker = (playerTracker == Manager.instance.playersInOrder.Count - 1) ? 0 : playerTracker + 1;
                }
            }
        }
    }

    IEnumerator RunStep(string methodName, Player player, int logged)
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
            yield return RunStep(chosenMethod, player, logged);
        }
        else
        {
            StartCoroutine((IEnumerator)dictionary[methodName].Invoke(this, new object[2] { player, logged }));

            while (runningMethod)
                yield return null;
            if (!runNextMethod) yield break;
        }
    }

    #endregion

#region Steps

    IEnumerator DrawCards(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.RequestDraw), RpcTarget.MasterClient, new object[2] {dataFile.numCards, logged});
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator GainCoins(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.GainCoin), RpcTarget.All, new object[2] { dataFile.numCoins, logged });
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator LoseCoins(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.LoseCoin), RpcTarget.All, new object[2] { dataFile.numCoins, logged });
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator TakeNeg(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.TakeNegCrown), RpcTarget.All, new object[2] { dataFile.numCrowns, logged });
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator RemoveNeg(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.RemoveNegCrown), RpcTarget.All, new object[2] { dataFile.numCrowns, logged });
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator DiscardHand(Player player, int logged)
    {
        yield return null;
        foreach (Card card in player.listOfHand)
            player.DiscardRPC(card, logged);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator MandatoryDiscard(Player player, int logged)
    {
        for (int i = 0; i<dataFile.numCards; i++)
        {
            Manager.instance.instructions.text = $"Discard a card ({dataFile.numCards-i} more).";
            yield return player.ChooseCard(player.listOfHand, false);
            player.DiscardRPC(player.chosenCard, logged);
        }
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator OptionalDiscard(Player player, int logged)
    {
        for (int i = 0; i < dataFile.numCards; i++)
        {
            Manager.instance.instructions.text = $"Discard a card ({dataFile.numCards - i} more)?";
            yield return player.ChooseCard(player.listOfHand, i == 0);

            if (player.chosenCard == null)
            {
                MultiFunction(nameof(StopInstructions), RpcTarget.All);
                break;
            }
            else
            {
                player.DiscardRPC(player.chosenCard, logged);
            }
        }
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator ChooseFromPlay(Player player, int logged)
    {
        yield return player.ChooseCard(player.listOfPlay, false);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator ChooseFromHand(Player player, int logged)
    {
        yield return player.ChooseCard(player.listOfHand, false);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator IgnoreUntilTurn(Player player, int logged)
    {
        yield return null;
        player.MultiFunction(nameof(player.IgnoreUntilTurn), RpcTarget.All, new object[1] { dataFile.numMisc });
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    #endregion

#region Booleans

    IEnumerator HandOrMore(Player player, int logged)
    {
        yield return null;
        if (!(player.listOfHand.Count <= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator MoneyOrLess(Player player, int logged)
    {
        yield return null;
        if (!(player.coins <= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator MoneyOrMore(Player player, int logged)
    {
        yield return null;
        if (!(player.coins >= dataFile.numMisc))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator LastUsedBrainstorm(Player player, int logged)
    {
        yield return null;
        if (player.lastUsedAction == null || !player.lastUsedAction.name.Equals("Brainstorm"))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator LastUsedStockpile(Player player, int logged)
    {
        yield return null;
        if (player.lastUsedAction == null || !player.lastUsedAction.name.Equals("Stockpile"))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator LastUsedUpgrade(Player player, int logged)
    {
        yield return null;
        if (player.lastUsedAction == null || !player.lastUsedAction.name.Equals("Upgrade"))
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator PlayedCardOrMore(Player player, int logged)
    {
        yield return null;
        if (player.cardsPlayed.Count >= dataFile.numMisc)
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator ChosenCard(Player player, int logged)
    {
        yield return null;
        if (player.chosenCard == null)
            MultiFunction(nameof(StopInstructions), RpcTarget.All);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator YesOrNo(Player player, int logged)
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
    void SetAllStats(int number)
    {
        float multiplier = (dataFile.numMisc > 0) ? dataFile.numMisc : -1 / dataFile.numMisc;
        dataFile.numCards = (int)Mathf.Floor(number * multiplier);
        dataFile.numCoins = (int)Mathf.Floor(number * multiplier);
        dataFile.numCrowns = (int)Mathf.Floor(number * multiplier);
        dataFile.numBatteries = (int)Mathf.Floor(number * multiplier);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    IEnumerator SetToHandSize(Player player, int logged)
    {
        yield return null;
        MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { player.listOfHand.Count });
    }

    IEnumerator SetToJunk(Player player, int logged)
    {
        yield return null;
        MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { player.listOfPlay.Where(card => card.name == "Junk").ToList().Count });
    }

    IEnumerator SetToNegCrowns(Player player, int logged)
    {
        yield return null;
        MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { player.negCrowns });
    }

    IEnumerator SetToCoins(Player player, int logged)
    {
        yield return null;
        MultiFunction(nameof(SetAllStats), RpcTarget.All, new object[1] { player.coins });
    }

    IEnumerator SetChosenToLastPlay(Player player, int logged)
    {
        if (player.cardsPlayed[^1] != null)
            yield return player.ChooseCard(new() { player.cardsPlayed[^1] }, false);
        MultiFunction(nameof(FinishedInstructions), RpcTarget.All);
    }

    #endregion

}
