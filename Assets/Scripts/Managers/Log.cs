using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MyBox;
using System.Text.RegularExpressions;
using Photon.Pun;
using System.Reflection;
using System;

[Serializable]
public class NextStep
{
    public UndoSource source;
    public Player player;
    public string instruction;
    public List<Card> cardsToRemember = new();
    public int numberToRemember { get; internal set; }
    public bool boolToRemember { get; internal set; }
    public Player canUndoThis;
    public int logged;

    internal NextStep(Player player, Player canUndoThis, UndoSource source, string instruction, List<Card> cardsToRemember, int numberToRemember, bool boolToRemember, int logged)
    {
        this.player = player;
        this.canUndoThis = canUndoThis;
        this.source = source;
        this.instruction = instruction;
        this.numberToRemember = numberToRemember;
        this.boolToRemember = boolToRemember;
        this.logged = logged;
    }
}

[RequireComponent(typeof(PhotonView))]
public class Log : MonoBehaviour
{

#region Variables

    public static Log instance;
    [ReadOnly] public PhotonView pv;

    [Foldout("Log", true)]
    Scrollbar scroll;
    [SerializeField] RectTransform RT;
    GridLayoutGroup gridGroup;
    float startingHeight;
    [SerializeField] LogText textBoxClone;
    public Dictionary<string, MethodInfo> dictionary = new();

    [Foldout("Undo", true)]
    List<NextStep> historyStack = new();
    int currentStep = 0;
    List<Button> undosInLog = new();
    bool nextUndoBar = false;
    Button undoButton;

    #endregion

#region Setup

    private void Awake()
    {
        gridGroup = RT.GetComponent<GridLayoutGroup>();
        startingHeight = RT.sizeDelta.y;
        scroll = this.transform.GetChild(1).GetComponent<Scrollbar>();
        instance = this;
        pv = GetComponent<PhotonView>();
        undoButton = GameObject.Find("Undo Button").GetComponent<Button>();
        undoButton.onClick.AddListener(() => DisplayUndoBar(true));
    }

    public void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        if (PhotonNetwork.IsConnected)
            pv.RPC(dictionary[methodName].Name, affects, parameters);
        else
            dictionary[methodName].Invoke(this, parameters);
    }

    public IEnumerator MultiEnumerator(string methodName, RpcTarget affects, object[] parameters = null)
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
        MethodInfo method = typeof(Log).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method != null && method.ReturnType == typeof(void) || method.ReturnType == typeof(IEnumerator))
            dictionary.Add(methodName, method);
    }

    #endregion

#region Add To Log

    public static string Article(string followingWord)
    {
        if (followingWord.StartsWith('A')
            || followingWord.StartsWith('E')
            || followingWord.StartsWith('I')
            || followingWord.StartsWith('O')
            || followingWord.StartsWith('U'))
        {
            return $"an {followingWord}";
        }
        else
        {
            return $"a {followingWord}";
        }
    }

    [PunRPC]
    public void AddText(string logText, int indent = 0)
    {
        //Debug.LogError($"{indent}: {logText}");
        if (indent < 0)
            return;

        /*
        if (historyStack.Count > 0)
            historyStack[^1].addedLogLines++;
        */
        LogText newText = Instantiate(textBoxClone, RT.transform);
        newText.textBox.text = "";
        for (int i = 0; i < indent; i++)
            newText.textBox.text += "     ";
        newText.textBox.text += string.IsNullOrEmpty(logText) ? "" : char.ToUpper(logText[0]) + logText[1..];

        newText.textBox.text = KeywordTooltip.instance.EditText(newText.textBox.text);
        if (nextUndoBar)
        {
            nextUndoBar = false;
            undosInLog.Insert(0, newText.GetComponent<Button>());
        }

        if (RT.transform.childCount >= (startingHeight / gridGroup.cellSize.y) - 1)
        {
            RT.sizeDelta = new Vector2(RT.sizeDelta.x, RT.sizeDelta.y + gridGroup.cellSize.y);

            if (scroll.value <= 0.2f)
            {
                scroll.value = 0;
                RT.transform.localPosition = new Vector3(RT.transform.localPosition.x, RT.transform.localPosition.y + gridGroup.cellSize.y / 2, 0);
            }
        }
    }

    #endregion

#region Steps

    public NextStep GetCurrentStep()
    {
        return historyStack[currentStep];
    }

    [PunRPC]
    public void Continue()
    {
        currentStep++;
    }

    #endregion

#region Undos

    void DisplayUndoBar(bool on)
    {
        undosInLog.RemoveAll(item => item == null);
        for (int i = 1; i <= undosInLog.Count; i++)
        {
            Button nextButton = undosInLog[i];
            nextButton.onClick.RemoveAllListeners();
            nextButton.interactable = on;
            nextButton.transform.GetChild(0).gameObject.SetActive(on);

            if (on)
            {
                int number = i;
                nextButton.onClick.AddListener(() => MultiFunction(nameof(UndoAmount), RpcTarget.All, new object[1] { number }));
            }
        }

        undoButton.onClick.RemoveAllListeners();
        undoButton.onClick.AddListener(() => DisplayUndoBar(!on));
    }

    public void AddStepRPC(Player player, Player canUndo, UndoSource source, string instruction, List<Card> cardsToRemember, int numberToRemember, bool boolToRemember, int logged)
    {
        if (PhotonNetwork.IsConnected)
        {
            int[] cardIDs = new int[cardsToRemember.Count];
            for (int i = 0; i < cardsToRemember.Count; i++)
                cardIDs[i] = cardsToRemember[i].pv.ViewID;
            MultiFunction(nameof(AddStep), RpcTarget.All, new object[8]
            { player.playerPosition, (canUndo == null) ? -1 : canUndo.playerPosition,
                source.pv.ViewID, instruction, cardIDs, numberToRemember, boolToRemember, logged }); 
        }
        else
        {
            AddStep(player, canUndo, source, instruction,
                cardsToRemember, numberToRemember, boolToRemember, logged);
        }
    }

    [PunRPC]
    void AddStep(int playerPosition, int undoPosition, int sourceID, string instruction, int[] cardIDs, int numberToRemember, bool boolToRemember, int logged)
    {
        List<Card> listOfCards = new();
        foreach (int ID in cardIDs)
            listOfCards.Add(PhotonView.Find(ID).GetComponent<Card>());

        AddStep(Manager.instance.playersInOrder[playerPosition],
            (undoPosition == -1) ? null : Manager.instance.playersInOrder[undoPosition],
            PhotonView.Find(sourceID).GetComponent<UndoSource>(), instruction,
            listOfCards, numberToRemember, boolToRemember, logged);     
    }

    void AddStep(Player player, Player canUndo, UndoSource source, string instruction, List<Card> cardsToRemember, int numberToRemember, bool boolToRemember, int logged)
    {
        historyStack.Insert(currentStep+1, new(player, canUndo, source, instruction, cardsToRemember, numberToRemember, boolToRemember, logged));
        if (canUndo != null)
            nextUndoBar = true;
    }

    [PunRPC]
    void UndoAmount(int amount)
    {
        DisplayUndoBar(false);
        Popup[] allPopups = FindObjectsOfType<Popup>();
        foreach (Popup popup in allPopups)
            Destroy(popup.gameObject);

        int tracker = 0;
        while (tracker < amount)
        {
            NextStep next = historyStack[currentStep];
            if (next.canUndoThis != null)
                tracker++;

            if (tracker == amount)
            {
                break;
            }
            else
            {
                next.source.UndoCommand(next);
                historyStack.RemoveAt(currentStep);
                currentStep--;
            }
        }

        /*
        int linesToDelete = 0;
        int currentStackCount = historyStack.Count - 1;

        UndoStep lastStep = null;

        for (int i = 0; i <= amount; i++)
        {
            UndoProcess process = historyStack[currentStackCount - i];
            historyStack.RemoveAt(currentStackCount - i);
            linesToDelete += process.addedLogLines;

            foreach (UndoStep step in process.listOfSteps)
            {
                lastStep = step;
                step.source.StopAllCoroutines();
                step.source.UndoCommand(step);
            }
        }

        for (int i = 1; i <= linesToDelete; i++)
        {
            Destroy(RT.transform.GetChild(RT.transform.childCount - i).gameObject);
        }

        //lastStep.source.methodDictionary[lastStep.instruction].Invoke(lastStep.source, new object[2] {-1, false});
        */
    }

    [PunRPC]
    public void ChangeNumber(int newNumber)
    {
        historyStack[currentStep].numberToRemember = newNumber;
    }

    #endregion
}
