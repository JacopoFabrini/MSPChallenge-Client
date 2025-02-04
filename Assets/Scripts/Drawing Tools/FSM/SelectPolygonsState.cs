﻿using System.Collections.Generic;
using UnityEngine;

namespace MSP2050.Scripts
{
	public class SelectPolygonsState : FSMState
	{
		PolygonLayer baseLayer;
		PlanLayer planLayer;

		bool selectingBox = false;
		HashSet<PolygonSubEntity> currentBoxSelection = new HashSet<PolygonSubEntity>();

		PolygonSubEntity previousHover = null;

		public SelectPolygonsState(FSM fsm, PlanLayer planLayer) : base(fsm)
		{
			this.planLayer = planLayer;
			this.baseLayer = planLayer.BaseLayer as PolygonLayer;
		}

		public override void EnterState(Vector3 currentMousePosition)
		{
			base.EnterState(currentMousePosition);

			UIManager.SetToolbarMode(ToolBar.DrawingMode.Edit);
			UIManager.ToolbarEnable(false, FSM.ToolbarInput.Delete);
			UIManager.ToolbarEnable(false, FSM.ToolbarInput.Recall);
			UIManager.ToolbarEnable(false, FSM.ToolbarInput.Abort);
			UIManager.SetActivePlanWindowInteractability(false);

			PolygonSubEntity hover = baseLayer.GetSubEntityAt(currentMousePosition) as PolygonSubEntity;

			if (hover != null)
			{
				HoveredSubEntity(hover, true);
			}

			previousHover = hover;
		}

		public override void LeftClick(Vector3 worldPosition)
		{
			PolygonSubEntity hover = baseLayer.GetSubEntityAt(worldPosition) as PolygonSubEntity;

			if (hover != null)
			{
				if (baseLayer.IsEnergyPolyLayer())
					fsm.SetCurrentState(new EditEnergyPolygonState(fsm, planLayer, new HashSet<PolygonSubEntity>() { hover }));
				else
					fsm.SetCurrentState(new EditPolygonsState(fsm, planLayer, new HashSet<PolygonSubEntity>() { hover }));
			}
		}

		public override void MouseMoved(Vector3 previousPosition, Vector3 currentPosition, bool cursorIsOverUI)
		{
			if (!selectingBox)
			{
				PolygonSubEntity hover = null;
				if (!cursorIsOverUI)
				{
					hover = baseLayer.GetSubEntityAt(currentPosition) as PolygonSubEntity;
					if (hover == null && baseLayer != null) { hover = baseLayer.GetSubEntityAt(currentPosition) as PolygonSubEntity; }
				}

				if (previousHover != null || hover != null)
				{
					if (previousHover != null)
					{
						HoveredSubEntity(previousHover, false);
					}

					if (hover != null)
					{
						HoveredSubEntity(hover, true);
					}
				}

				previousHover = hover;
			}
		}

		public override void StartedDragging(Vector3 dragStartPosition, Vector3 currentPosition)
		{
			selectingBox = true;
			currentBoxSelection = new HashSet<PolygonSubEntity>();

			BoxSelect.DrawBoxSelection(dragStartPosition, currentPosition);
		}

		public override void Dragging(Vector3 dragStartPosition, Vector3 currentPosition)
		{
			updateBoxSelection(dragStartPosition, currentPosition);
		}

		private void updateBoxSelection(Vector3 dragStartPosition, Vector3 currentPosition)
		{
			BoxSelect.DrawBoxSelection(dragStartPosition, currentPosition);

			HashSet<PolygonSubEntity> selectionsInBox = baseLayer.GetSubEntitiesInBox(dragStartPosition, currentPosition);

			foreach (PolygonSubEntity selectionInBox in selectionsInBox)
			{
				if (!currentBoxSelection.Contains(selectionInBox)) { HoveredSubEntity(selectionInBox, true); }
			}

			foreach (PolygonSubEntity currentlySelected in currentBoxSelection)
			{
				if (!selectionsInBox.Contains(currentlySelected)) { HoveredSubEntity(currentlySelected, false); }
			}

			currentBoxSelection = selectionsInBox;
		}

		public override void StoppedDragging(Vector3 dragStartPosition, Vector3 dragFinalPosition)
		{
			updateBoxSelection(dragStartPosition, dragFinalPosition);

			if (currentBoxSelection.Count > 0)
			{
				if (baseLayer.IsEnergyPolyLayer())
					fsm.SetCurrentState(new EditEnergyPolygonState(fsm, planLayer, currentBoxSelection));
				else
					fsm.SetCurrentState(new EditPolygonsState(fsm, planLayer, currentBoxSelection));
			}

			BoxSelect.HideBoxSelection();
			selectingBox = false;
		}

		public override void HandleKeyboardEvents()
		{
			if (Input.GetKeyDown(KeyCode.Return))
			{
				fsm.SetCurrentState(new StartCreatingPolygonState(fsm, planLayer));
			}
		}

		public override void HandleToolbarInput(FSM.ToolbarInput toolbarInput)
		{
			switch (toolbarInput)
			{
				case FSM.ToolbarInput.Create:
					fsm.SetCurrentState(new StartCreatingPolygonState(fsm, planLayer));
					break;
				case FSM.ToolbarInput.FindGaps:
					baseLayer.CreateInvertedLayer();
					break;
				case FSM.ToolbarInput.SelectAll:
					if (baseLayer.IsEnergyPolyLayer())
						fsm.SetCurrentState(new EditEnergyPolygonState(fsm, planLayer, new HashSet<PolygonSubEntity>((baseLayer as PolygonLayer).GetAllSubEntities())));
					else
						fsm.SetCurrentState(new EditPolygonsState(fsm, planLayer, new HashSet<PolygonSubEntity>((baseLayer as PolygonLayer).GetAllSubEntities())));
					break;
			}
		}

		public override void ExitState(Vector3 currentMousePosition)
		{
			if (previousHover != null)
			{
				HoveredSubEntity(previousHover, false);
			}

			foreach (PolygonSubEntity pse in currentBoxSelection)
			{
				pse.RedrawGameObject();
			}

			BoxSelect.HideBoxSelection();
		}
	}
}
