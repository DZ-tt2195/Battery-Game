using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MyBox;
using System.Linq;

public class Popup : MonoBehaviour
{
    public TMP_Text textbox;
    RectTransform textWidth;
    RectTransform imageWidth;
    Canvas canvas;

    [SerializeField] Button textButton;
    [SerializeField] Button cardButton;
    List<Button> buttonsInCollector = new List<Button>();
    [ReadOnly] public Player decidingPlayer;

    void Awake()
    {
        canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        textWidth = textbox.GetComponent<RectTransform>();
        imageWidth = this.transform.GetComponent<RectTransform>();
    }

    internal void StatsSetup(Player player, string header, Vector2 position)
    {
        decidingPlayer = player;
        this.textbox.text = KeywordTooltip.instance.EditText(header);
        this.transform.SetParent(canvas.transform);
        this.transform.localPosition = position;
        this.transform.localScale = new Vector3(1, 1, 1);
    }

    internal void DestroyButton(int sibling)
    {
        Button toDestroy = this.transform.GetChild(2).transform.GetChild(sibling).GetComponent<Button>();
        buttonsInCollector.Remove(toDestroy);
        Destroy(toDestroy.gameObject);

        if (this.transform.GetChild(2).transform.childCount <= 1)
            Destroy(this.gameObject);
    }

    internal void AddTextButton(string text)
    {
        Button nextButton = Instantiate(textButton, this.transform.GetChild(1));
        nextButton.transform.GetChild(0).GetComponent<TMP_Text>().text = KeywordTooltip.instance.EditText(text);

        nextButton.interactable = true;
        int buttonNumber = buttonsInCollector.Count;
        nextButton.onClick.AddListener(() => decidingPlayer.MadeDecision(null, buttonNumber));
        buttonsInCollector.Add(nextButton);

        imageWidth.sizeDelta = new Vector2(Mathf.Max(buttonsInCollector.Count, 2) * 350, imageWidth.sizeDelta.y);
        textWidth.sizeDelta = new Vector2(Mathf.Max(buttonsInCollector.Count, 2) * 350, textWidth.sizeDelta.y);

        for (int i = 0; i < buttonsInCollector.Count; i++)
        {
            Transform nextTransform = buttonsInCollector[i].transform;
            nextTransform.transform.localPosition = new Vector2((buttonsInCollector.Count - 1) * -150 + (300 * i), 0);
        }
    }

    internal void AddCardButton(Card card, float alpha)
    {
        Button nextButton = Instantiate(cardButton, this.transform.GetChild(1));
        CardLayout layout = nextButton.GetComponent<CardLayout>();
        layout.FillInCards(card.dataFile, card.background.color);
        layout.cg.alpha = alpha;

        nextButton.interactable = true;
        int buttonNumber = buttonsInCollector.Count;
        nextButton.onClick.AddListener(() => decidingPlayer.MadeDecision(card, buttonNumber));
        buttonsInCollector.Add(nextButton);

        imageWidth.sizeDelta = new Vector2(Mathf.Max(buttonsInCollector.Count, 2) * 350, imageWidth.sizeDelta.y);
        textWidth.sizeDelta = new Vector2(Mathf.Max(buttonsInCollector.Count, 2) * 350, textWidth.sizeDelta.y);

        for (int i = 0; i < buttonsInCollector.Count; i++)
        {
            Transform nextTransform = buttonsInCollector[i].transform;
            nextTransform.transform.localPosition = new Vector2((buttonsInCollector.Count - 1) * -150 + (300 * i), 0);
        }
    }

    internal void DisableAll()
    {
        foreach (Button x in buttonsInCollector)
        {
            try { x.enabled = false; }
            catch (NullReferenceException) { continue; }
        }
    }

    internal void EnableAll()
    {
        foreach (Button x in buttonsInCollector)
        {
            try { x.enabled = true; }
            catch (NullReferenceException) { continue; }
        }
    }

    internal void WaitForChoice()
    {
        if (buttonsInCollector.Count == 0)
            decidingPlayer.MadeDecision(null, -10);
    }
}
