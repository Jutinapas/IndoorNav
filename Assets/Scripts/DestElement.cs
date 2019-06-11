using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class DestElement : MonoBehaviour
{
    [SerializeField] int id;
	[SerializeField] Text dNameText;
	[SerializeField] Button dButton;

	public void Initialize (NodeShape dest, RectTransform listParent, UnityAction onButtonClick)
	{
        id = dest.id;
		dNameText.text = dest.name;
		gameObject.transform.SetParent (listParent);
		dButton.onClick.AddListener (onButtonClick);
	}

}
