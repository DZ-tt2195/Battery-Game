using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MyBox;
using System.Text.RegularExpressions;
using Photon.Pun;
using System.Reflection;

[RequireComponent(typeof(PhotonView))]
public class Log : MonoBehaviour
{    
    public static Log instance;
    Scrollbar scroll;
    [SerializeField] RectTransform RT;
    GridLayoutGroup gridGroup;
    float startingHeight;
    [SerializeField] TMP_Text textBoxClone;
    [ReadOnly] public PhotonView pv;
    public Dictionary<string, MethodInfo> dictionary = new();

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

    /*
    void Update()
    {
        #if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Space))
                AddText($"Test {RT.transform.childCount+1}");
        #endif
    }
    */

    /*
    public static string Substitute(Ability ability, Character user, Character target)
    {
        string sentence = ability.data.logDescription;
        sentence = sentence.Replace("THIS", user.data.myName);
        try{ sentence = sentence.Replace("TARGET", target.data.myName);} catch{do nothing}
        return sentence;
    }
    */
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

        TMP_Text newText = Instantiate(textBoxClone, RT.transform);
        newText.text = "";
        for (int i = 0; i < indent; i++)
            newText.text += "     ";
        newText.text += string.IsNullOrEmpty(logText) ? "" : char.ToUpper(logText[0]) + logText[1..];

        newText.text = KeywordTooltip.instance.EditText(newText.text);

        if (RT.transform.childCount >= (startingHeight / gridGroup.cellSize.y)-1)
        {
            RT.sizeDelta = new Vector2(RT.sizeDelta.x, RT.sizeDelta.y + gridGroup.cellSize.y);

            if (scroll.value <= 0.2f)
            {
                scroll.value = 0;
                RT.transform.localPosition = new Vector3(RT.transform.localPosition.x, RT.transform.localPosition.y + gridGroup.cellSize.y/2, 0);
            }
        }
    }
}
