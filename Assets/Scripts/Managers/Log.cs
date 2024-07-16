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
    public object[] infoToRemember;
    public Player canUndoThis;
    public int logged;

    internal NextStep(Player player, Player canUndoThis, UndoSource source, string instruction, object[] infoToRemember, int logged)
    {
        this.player = player;
        this.canUndoThis = canUndoThis;
        this.source = source;
        this.instruction = instruction;
        this.infoToRemember = infoToRemember;
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
    [ReadOnly] [SerializeField] List<NextStep> historyStack = new();
    [ReadOnly][SerializeField] int currentStep = 0;
    [ReadOnly][SerializeField] List<LogText> undosInLog = new();
    Player nextUndoBar = null;
    Button undoButton;

    #endregion

#region Setup

    private void Awake()
    {
        instance = this;
        pv = GetComponent<PhotonView>();
        undoButton = GameObject.Find("Undo Button").GetComponent<Button>();
        gridGroup = RT.GetComponent<GridLayoutGroup>();
        scroll = this.transform.GetChild(1).GetComponent<Scrollbar>();

        startingHeight = RT.sizeDelta.y;
        undoButton.onClick.AddListener(() => DisplayUndoBar());
        NextStep newStep = new(null, null, null, "", new object[0], -1);
        historyStack.Add(newStep);
    }

    public void MultiFunction(string methodName, RpcTarget affects, object[] parameters = null)
    {
        if (!dictionary.ContainsKey(methodName))
            AddToDictionary(methodName);

        MethodInfo info = dictionary[methodName];
        if (info.ReturnType == typeof(IEnumerator))
        {
            if (PhotonNetwork.IsConnected)
                pv.RPC(info.Name, affects, parameters);
            else
                StartCoroutine((IEnumerator)info.Invoke(this, parameters));
        }
        else if (info.ReturnType == typeof(void))
        {
            if (PhotonNetwork.IsConnected)
                pv.RPC(info.Name, affects, parameters);
            else
                info.Invoke(this, parameters);
        }
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

        LogText newText = Instantiate(textBoxClone, RT.transform);
        newText.textBox.text = "";
        for (int i = 0; i < indent; i++)
            newText.textBox.text += "     ";
        newText.textBox.text += string.IsNullOrEmpty(logText) ? "" : char.ToUpper(logText[0]) + logText[1..];

        newText.textBox.text = KeywordTooltip.instance.EditText(newText.textBox.text);
        if (nextUndoBar != null)
        {
            newText.canUndoThis = nextUndoBar;
            nextUndoBar = null;
            undosInLog.Insert(0, newText);
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
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            NextStep nextUp = GetCurrentStep();
            Debug.Log($"resolve step {currentStep}: {nextUp.instruction}");
            nextUp.source.MultiFunction(nextUp.instruction, RpcTarget.All, new object[2] { nextUp.logged, false });
        }
    }

    public void AddStepRPC(int insertion, Player player, Player canUndo, UndoSource source, string instruction, object[] infoToRemember, int logged)
    {
        if (PhotonNetwork.IsConnected)
        {
            pv.RPC(nameof(AddStep), RpcTarget.All, insertion, player == null ? -1 : player.playerPosition, canUndo == null ? -1 : canUndo.playerPosition, source == null ? -1 : source.pv.ViewID, instruction, infoToRemember, logged);
        }
        else
        {
            AddStep(insertion, player, canUndo, source, instruction, infoToRemember, logged);
        }
    }

    [PunRPC]
    void AddStep(int insertion, int playerPosition, int canUndo, int source, string instruction, object[] infoToRemember, int logged)
    {
        AddStep(insertion, playerPosition < 0 ? null : Manager.instance.playersInOrder[playerPosition], canUndo < 0 ? null : Manager.instance.playersInOrder[canUndo], source < 0 ? null : PhotonView.Find(source).GetComponent<UndoSource>(), instruction, infoToRemember, logged);
    }

    void AddStep(int insertion, Player player, Player canUndo, UndoSource source, string instruction, object[] infoToRemember, int logged)
    {
        NextStep newStep = new(player, canUndo, source, instruction, infoToRemember, logged);
        if (canUndo != null)
            nextUndoBar = canUndo;

        try
        {
            historyStack.Insert(currentStep + insertion, newStep);
        }
        catch
        {
            historyStack.Add(newStep);
        }
    }

#endregion

#region Undos

    void DisplayUndoBar()
    {
        if (undosInLog.Count > 0)
        {
            bool flash = undosInLog[^1].undoBar.gameObject.activeSelf;

            undosInLog.RemoveAll(item => item == null);
            for (int i = 0; i < undosInLog.Count; i++)
            {
                LogText next = undosInLog[i];
                next.button.onClick.RemoveAllListeners();
                next.button.interactable = flash;
                next.undoBar.gameObject.SetActive(false);

                if (flash && next.canUndoThis.InControl())
                {
                    next.undoBar.gameObject.SetActive(flash);
                    int number = i;
                    next.button.onClick.AddListener(() => MultiFunction(nameof(UndoAmount), RpcTarget.All, new object[2] { number, next.transform.GetSiblingIndex() }));
                }
            }
        }
    }

    [PunRPC]
    void UndoAmount(int amount, int logDelete)
    {
        StartCoroutine(CarryVariables.instance.TransitionImage(1f));
        undosInLog[^1].undoBar.gameObject.SetActive(false);
        DisplayUndoBar();

        Popup[] allPopups = FindObjectsOfType<Popup>();
        foreach (Popup popup in allPopups)
            Destroy(popup.gameObject);

        foreach (Card card in Manager.instance.cardIDs)
        {
            card.button.interactable = false;
            card.button.onClick.RemoveAllListeners();
        }

        Debug.Log($"{RT.transform.childCount}, {logDelete}");
        for (int i = RT.transform.childCount; i>logDelete; i--)
        {
            Destroy(RT.transform.GetChild(i-1).gameObject);
        }

        int tracker = -1;

        while (tracker < amount)
        {
            NextStep next = historyStack[currentStep];
            Debug.Log($"undo step {currentStep}: {next.instruction}");

            if (next.canUndoThis != null)
                tracker++;

            if (tracker == amount)
            {
                currentStep--;
                nextUndoBar = next.canUndoThis;
                Continue();
                break;
            }
            else
            {
                next.source.MultiFunction(next.instruction, RpcTarget.All, new object[2] { next.logged, true });
                historyStack.RemoveAt(currentStep);
                currentStep--;
            }
        }
    }

    #endregion

}
