﻿using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MSP2050.Scripts
{
	public class ActivePlanLayer : MonoBehaviour
	{
		public Toggle toggle;
		public Image layerIcon, editingIcon;
		public TextMeshProUGUI layerNameText;

		public void SetToLayer(PlanLayer layer)
		{
			//Set visuals
			layerNameText.text = layer.BaseLayer.ShortName;
			layerIcon.sprite = LayerInterface.GetIconStatic(layer.BaseLayer.SubCategory);

			//Button callback
			toggle.onValueChanged.AddListener((value) =>
			{
				if(value) {
					Main.FSM.AbortCurrentState();
					InterfaceCanvas.Instance.activePlanWindow.ActivePlanLayerCallback(layer);
				}
			});
		}

		public void SetSelected(bool value)
		{
			editingIcon.gameObject.SetActive(value);
		}
	}
}
