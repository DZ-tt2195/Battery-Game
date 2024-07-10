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
public class UndoProcess
{
    public Player canAskUndo;
    public List<UndoStep> listOfSteps = new();
    public int addedLogLines = 0;

    public UndoProcess(int playerPosition)
    {
        this.canAskUndo = Manager.instance.playersInOrder[playerPosition];
    }
}

[Serializable]
public class UndoStep
{
    public UndoSource source;
    public Player user;
    public string instruction;
    public List<Card> cardsToRemember = new();
    public int numberToRemember { get; internal set; }

    public UndoStep(int playerPosition, int sourceID, string instruction)
    {
        this.user = Manager.instance.playersInOrder[playerPosition];
        this.source = PhotonView.Find(sourceID).GetComponent<UndoSource>();
        this.instruction = instruction;
    }

    public UndoStep(Player user, UndoSource source, string instruction)
    {
        this.user = user;
        this.source = source;
        this.instruction = instruction;
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
    List<UndoProcess> historyStack = new();
    List<Button> undosInLog = new();
    bool nextUndoBar = false;

    #endregion

#region Setup

    private void Awake()
    {
        gridGroup = RT.GetComponent<GridLayoutGroup>();
        startingHeight = RT.sizeDelta.y;
        scroll = this.transform.GetChild(1).GetComponent<Scrollbar>();
        instance = this;
        pv = GetComponent<PhotonView>();
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

        if (historyStack.Count > 0)
            historyStack[^1].addedLogLines++;

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

#region Undos

    void DisplayUndoBar(bool on)
    {
        undosInLog.RemoveAll(item => item == null);
        for (int i = 0; i < undosInLog.Count; i++)
        {
            Button nextButton = undosInLog[i];
            nextButton.onClick.RemoveAllListeners();
            nextButton.interactable = on;
            nextButton.transform.GetChild(0).gameObject.SetActive(on);

            if (on)
            {
                int number = i;
                nextButton.onClick.AddListener(() => MultiFunction(nameof(Undo), RpcTarget.All, new object[1] { number }));
            }
        }

        //undoButton.onClick.RemoveAllListeners();
        //undoButton.onClick.AddListener(() => DisplayUndoBar(!on));
    }

    [PunRPC]
    public void AddUndoPoint(int playerPosition)
    {
        historyStack.Add(new(playerPosition));
        instance.nextUndoBar = true;
    }

    public void AddUndoStep(Player user, UndoSource source, string instruction)
    {
        if (PhotonNetwork.IsConnected)
        {
            pv.RPC(nameof(AddStepToStack), RpcTarget.All, new object[3] { user.pv.ViewID, source.pv.ViewID, instruction });
        }
        else
        {
            AddStepToStack(user, source, instruction);
        }
    }

    [PunRPC]
    void AddStepToStack(int userID, int sourceID, string instruction)
    {
        AddStepToStack(PhotonView.Find(userID).GetComponent<Player>(), PhotonView.Find(sourceID).GetComponent<UndoSource>(), instruction);
    }

    void AddStepToStack(Player user, UndoSource source, string instruction)
    {
        UndoProcess currentProcess = historyStack[^1];

        if (currentProcess.listOfSteps.Count == 0 || currentProcess.listOfSteps[0].instruction != "")
        {
            currentProcess.listOfSteps.Insert(0, new(user, source, instruction));
        }
        else
        {
            currentProcess.listOfSteps[0] = new(user, source, instruction);
        }
    }

    [PunRPC]
    void Undo(int amount)
    {
        DisplayUndoBar(false);
        int linesToDelete = 0;
        int currentStackCount = historyStack.Count - 1;

        for (int i = 0; i <= amount; i++)
        {
            UndoProcess process = historyStack[currentStackCount - i];
            historyStack.RemoveAt(currentStackCount - i);
            linesToDelete += process.addedLogLines;

            foreach (UndoStep step in process.listOfSteps)
                StartCoroutine(step.source.UndoCommand(step));
        }

        for (int i = 1; i <= linesToDelete; i++)
        {
            Destroy(RT.transform.GetChild(RT.transform.childCount - i).gameObject);
        }
    }

    [PunRPC]
    public void AddNumber(int number)
    {
        UndoStep currentStep = historyStack[^1].listOfSteps[0];
        currentStep.numberToRemember = number;
    }

    public void AddCardToUndo(Card card)
    {
        if (PhotonNetwork.IsConnected)
        {
            MultiFunction(nameof(AddCardToList), RpcTarget.All, new object[1] { card.pv.ViewID });
        }
        else
        {
            AddCardToList(card);
        }
    }

    [PunRPC]
    void AddCardToList(int ID)
    {
        AddCardToList(PhotonView.Find(ID).GetComponent<Card>());
    }

    void AddCardToList(Card card)
    {
        UndoStep currentStep = historyStack[^1].listOfSteps[0];
        currentStep.cardsToRemember.Add(card);
    }

    public UndoStep GetNewStep()
    {
        return historyStack[^1].listOfSteps[0];
    }

    #endregion

}
