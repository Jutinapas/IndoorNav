using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class DestInfoElement : MonoBehaviour
{
    [HideInInspector] int id;
	[SerializeField] Text dNameText;
	[SerializeField] Toggle dToggle;

	public void Initialize (Destination dest, ToggleGroup toggleGroup,
	                       RectTransform listParent, UnityAction<bool> onToggleChanged)
	{
        id = dest.id;
		dNameText.text = dest.name;
		dToggle.group = toggleGroup;
		gameObject.transform.SetParent (listParent);
		dToggle.onValueChanged.AddListener (onToggleChanged);
	}

}
