﻿using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MSP2050.Scripts
{
	public class Tooltip : MonoBehaviour
	{
		private float padding = 0;

		private Image tooltipContainer;
		public TextMeshProUGUI tooltipText;

		private bool isActive = false;

		public delegate void OnShowTooltip();
		public OnShowTooltip showTooltipCallback;

		private Vector2? offset = null;
	
		public void Initialise(string text, OnShowTooltip onShowTooltip, Vector2? offset = null)
		{
			this.offset = offset;
			tooltipContainer = this.GetComponent<Image>();

			// To offset the tooltips properly
			padding = TooltipManager.GetPadding();

			showTooltipCallback = onShowTooltip;

			SetText(text);
			ShowToolTip();
			gameObject.SetActive(false);
		}

		public void SetText(string newText)
		{
			tooltipText.text = newText;
			//animator.SetBool("Fade", false);
		}

		public string GetText()
		{
			return tooltipText.text;
		}

		private void SetPosition()
		{
			RectTransform rect = GetComponent<RectTransform>();
			float scale = 1f;
			if (InterfaceCanvas.Instance != null)
				scale = InterfaceCanvas.Instance.canvas.scaleFactor;
			else
				scale = GetComponentInParent<Canvas>().scaleFactor;

			float tooltipWidth = (tooltipText.preferredWidth + padding) * scale;
			float tooltipHeight = (tooltipText.preferredHeight + padding) * scale;
		
			float newX = Input.mousePosition.x / scale;
			float newY = Input.mousePosition.y / scale;
			if (newY > (Screen.height - tooltipHeight) / scale)
			{
				rect.pivot = new Vector2(0, 1f);
				newY -= 16f / scale;
			}
			else
				rect.pivot = Vector2.zero;
		
			Vector2 offsetToUse = (offset ?? new Vector2(0.0f, 0.0f)) / scale;
			rect.anchoredPosition = transform.position = new Vector2(
				Mathf.Clamp(newX, 0f, (Screen.width - tooltipWidth) / scale) + offsetToUse.x,
				Mathf.Max(newY, 0f) + offsetToUse.y);
			//TODO: set anchor min, max and position based on the quadrant of the screen the mouse is on
		}

		public void ShowToolTip()
		{
			isActive = true;
			gameObject.SetActive(true);
		

			if (showTooltipCallback != null)
			{
				showTooltipCallback.Invoke();
			}
		}

		public void HideTooltip()
		{
			gameObject.SetActive(false);
		}


		protected void LateUpdate()
		{
			if (isActive)
			{
				SetPosition();
			}
		}
	}
}
