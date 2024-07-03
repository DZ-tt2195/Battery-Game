using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MyBox;
using TMPro;

public class CardLayout : MonoBehaviour, IPointerClickHandler
{
    CardData dataFile;
    [ReadOnly] public CanvasGroup cg;
    [SerializeField] Image background;
    TMP_Text titleText;
    TMP_Text descriptionText;
    TMP_Text artText;
    TMP_Text coinText;
    TMP_Text crownText;
    Image artBox;
    List<CanvasGroup> listOfClocks = new(); 

    private void Awake()
    {
        cg = transform.Find("Canvas Group").GetComponent<CanvasGroup>();
        titleText = cg.transform.Find("Title").GetComponent<TMP_Text>();
        descriptionText = cg.transform.Find("Description").GetComponent<TMP_Text>();
        artBox = cg.transform.Find("Art Box").GetComponent<Image>();
        artText = cg.transform.Find("Art Credit").GetComponent<TMP_Text>();

        try
        {
            coinText = cg.transform.Find("Coin").GetComponent<TMP_Text>();
            crownText = cg.transform.Find("Crown").GetComponent<TMP_Text>();
        }
        catch
        {

        }

        try
        {
            Transform clockTransform = cg.transform.Find("Bundle of Clocks");
            foreach (Transform child in clockTransform)
                listOfClocks.Add(child.GetComponent<CanvasGroup>());
        }
        catch
        {

        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            RightClickInfo();
        }
    }

    public void FillInCards(CardData dataFile, Sprite sprite, Color color)
    {
        this.dataFile = dataFile;

        try
        {
            background.color = color;
        }
        catch
        {
            Debug.LogError($"{this.name} has no background");
        }
        titleText.text = dataFile.cardName;
        descriptionText.text = dataFile.textBox;
        descriptionText.text = KeywordTooltip.instance.EditText(descriptionText.text);

        artText.text = dataFile.artCredit;
        artBox.sprite = sprite;
        artBox.color = Color.red;

        if (coinText != null)
        {
            if (dataFile.coinCost >= 0)
            {
                coinText.gameObject.SetActive(true);
                coinText.text = $"{dataFile.coinCost} Coin";
                coinText.text = KeywordTooltip.instance.EditText(coinText.text);
            }
            else
            {
                coinText.gameObject.SetActive(false);
            }
        }

        if (crownText != null)
        {
            if (dataFile.scoringCrowns >= 0)
            {
                crownText.gameObject.SetActive(true);
                crownText.text = $"{dataFile.scoringCrowns} Pos Crown";
                crownText.text = KeywordTooltip.instance.EditText(crownText.text);
            }
            else
            {
                crownText.gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < listOfClocks.Count; i++)
        {
            if (dataFile.eventTimes.Count == 0)
                listOfClocks[i].alpha = 0;
            else if (dataFile.eventTimes.Contains(i + 1))
                listOfClocks[i].alpha = 1;
            else
                listOfClocks[i].alpha = 0.33f;
        }
    }

    void RightClickInfo()
    {
        CarryVariables.instance.RightClickDisplay(cg.alpha, (artBox != null) ? artBox.sprite : null, this.dataFile, background.color);
    }
}
