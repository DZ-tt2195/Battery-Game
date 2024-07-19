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
using Photon.Realtime;

[Serializable]
public class NextStep
{
    public UndoSource source;
    public Player player;
    public string instruction;
    public object[] infoToRemember;
    public Player canUndoThis;
    public int logged;

    internal NextStep(Player player, UndoSource source, string instruction, object[] infoToRemember, int logged)
    {
        this.player = player;
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
        [SerializeField] LogText textBoxClone;
        Vector2 startingSize;
        Vector2 startingPosition;

    [Foldout("Undo", true)]
        [ReadOnly] [SerializeField] int currentStep = 0;
        [ReadOnly] [SerializeField] List<LogText> undosInLog = new();
        [ReadOnly] [SerializeField] List<NextStep> historyStack = new();
        Player nextUndoBar = null;
        Button undoButton;
        public Dictionary<string, MethodInfo> dictionary = new();

    #endregion

#region Setup

    private void Awake()
    {
        instance = this;
        pv = GetComponent<PhotonView>();
        undoButton = GameObject.Find("Undo Button").GetComponent<Button>();
        gridGroup = RT.GetComponent<GridLayoutGroup>();
        scroll = this.transform.GetChild(1).GetComponent<Scrollbar>();

        startingSize = RT.sizeDelta;
        startingPosition = RT.transform.localPosition;
        undoButton.onClick.AddListener(() => DisplayUndoBar(true));
        NextStep newStep = new(null, null, "", new object[0], -1);
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
        newText.name = $"Log {RT.transform.childCount}";
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

        ChangeScrolling();
    }

    void ChangeScrolling()
    {
        int goPast = Mathf.FloorToInt((startingSize.y / gridGroup.cellSize.y) - 1);
        //Debug.Log($"{RT.transform.childCount} vs {goPast}");
        if (RT.transform.childCount > goPast)
        {
            RT.sizeDelta = new Vector2(startingSize.x, startingSize.y + ((RT.transform.childCount - goPast) * gridGroup.cellSize.y));
            if (scroll.value <= 0.2f)
            {
                RT.transform.localPosition = new Vector3(RT.transform.localPosition.x, RT.transform.localPosition.y + gridGroup.cellSize.y / 2, 0);
                scroll.value = 0;
            }
        }
        else
        {
            RT.sizeDelta = startingSize;
            RT.transform.localPosition = startingPosition;
            scroll.value = 0;
        }
    }

    private void Update()
    {
        if (Application.isEditor && Input.GetKeyDown(KeyCode.Space))
            AddText($"test {RT.transform.childCount}");
    }

    #endregion

#region Steps

    public NextStep GetCurrentStep()
    {
        return historyStack[currentStep];
    }

    [PunRPC]
    public void CurrentStepNeedsDecision(int playerPosition)
    {
        historyStack[currentStep].canUndoThis = Manager.instance.playersInOrder[playerPosition];
        nextUndoBar = Manager.instance.playersInOrder[playerPosition];
    }

    [PunRPC]
    public void Continue()
    {
        if (currentStep < historyStack.Count-1)
        {
            currentStep++;
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
            {
                NextStep nextUp = GetCurrentStep();
                //Debug.Log($"resolve step {currentStep}: {nextUp.instruction}");
                nextUp.source.MultiFunction(nextUp.instruction, RpcTarget.All, new object[2] { nextUp.logged, false });
            }
        }
    }

    public void AddStepRPC(int insertion, Player player, UndoSource source, string instruction, object[] infoToRemember, int logged)
    {
        if (PhotonNetwork.IsConnected)
        {
            pv.RPC(nameof(AddStep), RpcTarget.All, insertion, player == null ? -1 : player.playerPosition, source == null ? -1 : source.pv.ViewID, instruction, infoToRemember, logged);
        }
        else
        {
            AddStep(insertion, player, source, instruction, infoToRemember, logged);
        }
    }

    [PunRPC]
    void AddStep(int insertion, int playerPosition, int source, string instruction, object[] infoToRemember, int logged)
    {
        AddStep(insertion, playerPosition < 0 ? null : Manager.instance.playersInOrder[playerPosition],
            source < 0 ? null : PhotonView.Find(source).GetComponent<UndoSource>(), instruction, infoToRemember, logged);
    }

    void AddStep(int insertion, Player player, UndoSource source, string instruction, object[] infoToRemember, int logged)
    {
        NextStep newStep = new(player, source, instruction, infoToRemember, logged);

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

    void DisplayUndoBar(bool flash)
    {
        undosInLog.RemoveAll(item => item == null);

        if (undosInLog.Count > 0)
        {
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

        if (flash)
            Invoke(nameof(WaitSome), 5f);
    }

    void WaitSome()
    {
        DisplayUndoBar(false);
    }

    [PunRPC]
    void UndoAmount(int amount, int logDelete)
    {
        Manager.instance.StopAllCoroutines();
        foreach (Player player in Manager.instance.playersInOrder)
        {
            player.ResetEvent();
        }

        Popup[] allPopups = FindObjectsOfType<Popup>();
        foreach (Popup popup in allPopups)
            Destroy(popup.gameObject);

        foreach (Card card in Manager.instance.cardIDs)
        {
            card.button.interactable = false;
            card.button.onClick.RemoveAllListeners();
            card.border.gameObject.SetActive(false);
            card.ResetEvent();
        }

        StartCoroutine(CarryVariables.instance.TransitionImage(1f));
        DisplayUndoBar(false);

        for (int i = RT.transform.childCount; i>logDelete; i--)
            Destroy(RT.transform.GetChild(i-1).gameObject);
        ChangeScrolling();

        int tracker = -2;

        while (tracker < amount)
        {
            NextStep next = historyStack[currentStep];
            //Debug.Log($"undo step {currentStep}: {next.instruction}");

            next.source.MultiFunction(next.instruction, RpcTarget.All, new object[2] { next.logged, true });

            if (next.canUndoThis != null)
                tracker++;

            if (tracker == amount)
            {
                Debug.Log($"continue going at step {currentStep}: {next.instruction}");
                nextUndoBar = next.canUndoThis;
                currentStep--;
                Continue();
                break;
            }
            else
            {
                historyStack.RemoveAt(currentStep);
                currentStep--;
            }
        }
    }

#endregion

}
