
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class InventoryItem : MonoBehaviour
{
    public ItemSO itemScriptableObject;

    public int stackCurrent = 1;
    public int stackMax;
    public float totalWeight;
    public float timeSinceLastUse;
    [SerializeField] private List<int> durabilityList = new List<int>();
    public List<int> DurabilityList
    {
        get { return durabilityList; }
        set { durabilityList = value; }
    }  
    public Image IconImage
    {
        get { return iconImage; }
        set { iconImage = value; }
    }    
    public Text StackText
    {
        get { return stackText; }
        set { stackText = value; }
    }
    [SerializeField] Image iconImage;
    [SerializeField] Text stackText;


    private void Start()
    {
        stackMax = itemScriptableObject.StackMax;
    }

    void Update()
    {
        iconImage.sprite = itemScriptableObject.Icon;

        if (stackMax >= 1)
        {
            stackText.text = stackCurrent.ToString();
        }
    }
}


