﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

namespace MSP2050.Scripts
{
	public class Plan : IComparable<Plan>
	{
		public delegate void PlanLockAction(Plan plan);

		public enum PlanState { DESIGN = 0, CONSULTATION = 1, APPROVAL = 2, APPROVED = 3, IMPLEMENTED = 4, DELETED = 5 };

		public int creationBatchCallID; //ID of the PostPlan call in the batch
		public int ID = -1;
		public string Name;
		public string Description;
		public int StartTime;
		public int ConstructionStartTime;
		public PlanState State;
		public int Country;
		public int LockedBy;

		public List<PlanLayer> PlanLayers { get; private set; }
		public Dictionary<int, EPlanApprovalState> countryApproval;
		public Dictionary<string, APolicyPlanData> m_policies { get; private set; } //These are PolicyPlanData

		public List<PlanMessage> PlanMessages { get; private set; }
		private static HashSet<int> m_receivedPlanMessages = new HashSet<int>();
		public delegate void OnMessageReceived(PlanMessage a_message);
		public event OnMessageReceived OnMessageReceivedCallback;

		private bool requestingLock;

		public Plan()
		{
			State = Plan.PlanState.DESIGN;
			Country = SessionManager.Instance.CurrentUserTeamID;
		}

		public Plan(PlanObject planObject, Dictionary<AbstractLayer, int> layerUpdateTimes)
		{
			//=================================== BASE INFO =====================================
			ID = planObject.id;
			Name = planObject.name;
			Description = planObject.description;
			StartTime = planObject.startdate;
			State = StringToPlanState(planObject.state);
			Country = planObject.country;

			//Set locked state
			int lockedByUser = -1;
			if (planObject.locked != null)
				lockedByUser = Util.ParseToInt(planObject.locked);
			LockedBy = lockedByUser;

			//Set required approval
			if (planObject.votes.Count > 0)
			{
				countryApproval = new Dictionary<int, EPlanApprovalState>();
				foreach (ApprovalObject obj in planObject.votes)
					countryApproval.Add(obj.country, obj.vote);
			}
			PlanMessages = new List<PlanMessage>();

			//=================================== PLANLAYERS =====================================

			//Create new plan layers
			PlanLayers = new List<PlanLayer>();
			foreach (PlanLayerObject layer in planObject.layers)
			{
				PlanLayer newPlanLayer = new PlanLayer(this, layer, layerUpdateTimes);
				PlanLayers.Add(newPlanLayer);
				newPlanLayer.DrawGameObjects();
			}

			//Determine contruction time and add to base layer
			int maxConstructionTime = 0;
			foreach (PlanLayer planLayer in PlanLayers)
			{
				if (State != PlanState.DELETED)
					planLayer.BaseLayer.AddPlanLayer(planLayer);
				if (planLayer.BaseLayer.AssemblyTime > maxConstructionTime)
					maxConstructionTime = planLayer.BaseLayer.AssemblyTime;
			}
			ConstructionStartTime = StartTime - maxConstructionTime;

			//=================================== PLAN TYPE =====================================

			PolicyManager.Instance.RunPlanUpdate(planObject.policies, this, APolicyLogic.EPolicyUpdateStage.General);
		}

		public bool IsRequestingLock()
		{
			return requestingLock;
		}

		public bool IsLocked
		{
			get { return LockedBy != -1; }
		}

		public void AttemptLock(PlanLockAction actionOnSuccess, PlanLockAction actionOnFail)
		{
			if (PlanManager.Instance.UserHasPlanLocked(SessionManager.Instance.CurrentSessionID))
			{
				if (actionOnFail != null)
					actionOnFail(this);
				DialogBoxManager.instance.NotificationWindow("Lock failed", "You already have another plan locked.", () => { });
				return;
			}

			requestingLock = true;
			NetworkForm form = new NetworkForm();
			form.AddField("id", ID);
			form.AddField("user", SessionManager.Instance.CurrentSessionID.ToString());
			ServerCommunication.Instance.DoRequest<string>(Server.LockPlan(), form, (_) => HandleAttemptLockSuccess(actionOnSuccess), (r,m) => HandleAttemptLockFailure(actionOnFail, r, m));
		}

		private void HandleAttemptLockSuccess(PlanLockAction actionOnSuccess)
		{
			LockedBy = SessionManager.Instance.CurrentSessionID;
			PlanDetails.LockStateChanged(this, IsLocked);
			PlanManager.Instance.PlanLockUpdated(this);
			if (actionOnSuccess != null)
				actionOnSuccess(this);
			requestingLock = false;
		}

		private void HandleAttemptLockFailure(PlanLockAction actionOnFail, ARequest request, string message)
		{
			if (request.retriesRemaining > 0)
			{
				Debug.Log($"Request failed with message: {message}.. Retrying {request.retriesRemaining} more times.");
				ServerCommunication.Instance.RetryRequest(request);
			}
			else
			{
				requestingLock = false;
				Debug.Log("Failed to Lock " + Name + ". Error message: " + message);

				if (actionOnFail != null)
					actionOnFail(this);
				DialogBoxManager.instance.NotificationWindow("Lock failed", "The plan was locked by another user and could not be modified.", () => { });
			}
		}

		public void AttemptUnlock()
		{
			AttemptUnlock(false, null);
		}

		public bool HasPolicyErrors()
		{
			foreach (var kvp in m_policies)
			{
				if (kvp.Value.logic.HasError(kvp.Value))
					return true;
			}
			return false;
		}

		public void UpdatePlan(PlanObject updatedData, Dictionary<AbstractLayer, int> layerUpdateTimes)
		{
			//=================================== BASE INFO UPDATE =====================================
			bool nameOrDescriptionChanged = false, timeChanged = false, stateChanged = false, forceMonitorUpdate = false;
			bool inTimelineBefore = ShouldBeVisibleInTimeline;

			//Handle name
			if (updatedData.name != Name)
			{
				Name = updatedData.name;
				nameOrDescriptionChanged = true;
			}

			//Handle description
			if (updatedData.description != Description)
			{
				Description = updatedData.description;
				nameOrDescriptionChanged = true;
			}

			//Handle locks
			int lockedByUser = -1;
			if (updatedData.locked != null)
				lockedByUser = Util.ParseToInt(updatedData.locked);
			if (lockedByUser != LockedBy)
			{
				LockedBy = lockedByUser;
				PlanManager.Instance.PlanLockUpdated(this);
				PlanDetails.LockStateChanged(this, IsLocked);
				stateChanged = true;
			}

			//Handle state
			PlanState oldState = State;
			State = StringToPlanState(updatedData.state);
			if (oldState != State)
			{
				if (State != PlanState.DESIGN)
				{
					//Cancel editing if we were editing it before
					if (Main.CurrentlyEditingPlan != null && Main.CurrentlyEditingPlan.ID == updatedData.id)
					{
						PlanDetails.instance.CancelEditingContent();

						if (State == PlanState.DELETED)
						{
							DialogBoxManager.instance.NotificationWindow("Plan archived", "The plan's construction start time has passed. The plan was archived and changes have not been saved. Change the plan's implementation date and return it to the design phase and continue editing.", () => { });
						}
						else
						{
							DialogBoxManager.instance.NotificationWindow("Plan state changed", "Another player moved the plan out of the design state while you were editing. The current changes have not been saved. Change the plan state back to design to start editing again.", () => { });
						}
					}
				}

				if (State == PlanState.DELETED) //was deleted, disable and remove plan layers
				{
					//Stop viewing plan if we were before
					if (PlanManager.Instance.planViewing != null)
						if (PlanManager.Instance.planViewing.ID == ID)
							PlanManager.Instance.HideCurrentPlan();

					//Remove planlayers from their respective layers (AFTER REDRAWING)
					foreach (PlanLayer layer in PlanLayers)
					{
						layer.BaseLayer.RemovePlanLayer(layer);
						layer.issues = null;
					}
				}
				else if (State == PlanState.IMPLEMENTED)
				{
					foreach (PlanLayer layer in PlanLayers)
						layer.issues = null;
					//Stop viewing plan if we were before
					if (PlanManager.Instance.planViewing != null)
						if (PlanManager.Instance.planViewing.ID == ID)
							PlanManager.Instance.HideCurrentPlan();
				}
				else if (oldState == PlanState.DELETED) //was deleted before, re-enable and add layers to base
				{
					foreach (PlanLayer layer in PlanLayers)
					{
						layer.BaseLayer.AddPlanLayer(layer);
					}
				}

				stateChanged = true;
			}

			//Handle approval
			if (updatedData.votes == null)
			{
				if (countryApproval != null)
					stateChanged = true;
				countryApproval = null;
			}
			else
			{
				Dictionary<int, EPlanApprovalState> newCountryApproval = new Dictionary<int, EPlanApprovalState>();
				foreach (ApprovalObject obj in updatedData.votes)
				{
					if (countryApproval == null || !countryApproval.ContainsKey(obj.country) || countryApproval[obj.country] != obj.vote)
						stateChanged = true; //Approval value is new or different
					newCountryApproval.Add(obj.country, obj.vote);
				}
				//Approval was already new or different OR approval has been removed
				stateChanged = stateChanged || (countryApproval != null && newCountryApproval.Count != countryApproval.Count);
				countryApproval = newCountryApproval;
			}

			//Handle time
			int oldStartTime = StartTime;
			if (updatedData.startdate != StartTime)
			{
				StartTime = updatedData.startdate;
				PlanManager.Instance.UpdatePlanTime(this);
				if (State != PlanState.DELETED)
					foreach (PlanLayer planLayer in PlanLayers)
						planLayer.BaseLayer.UpdatePlanLayerTime(planLayer);
				timeChanged = true;
			}

			bool typeChanged = false;
			bool layersChanged = false;
			//PlanLayerUpdateTracker planLayerUpdateTracker = new PlanLayerUpdateTracker();

			//Do not update if we have the plan locked, no one could have changed it.
			//This persists if we stop editing until all data has been sent.
			if (!IsLockedByUs)
			{
				//=================================== GEOMETRY UPDATE =====================================
				//Keep track of planlayers that are not present after the update anymore
				HashSet<PlanLayer> removedPlanLayers = new HashSet<PlanLayer>(PlanLayers);
				foreach (PlanLayerObject updatedLayer in updatedData.layers)
				{
					PlanLayer planLayer = getPlanLayerForID(updatedLayer.layerid);
					if (planLayer == null)
					{
						//Create new planLayer
						planLayer = new PlanLayer(this, updatedLayer, layerUpdateTimes);
						PlanLayers.Add(planLayer);
						if (State != PlanState.DELETED)
							planLayer.BaseLayer.AddPlanLayer(planLayer);
						planLayer.DrawGameObjects();
						layersChanged = true;
					}
					else
					{
						//Update existing PlanLayer
						planLayer.UpdatePlanLayer(updatedLayer, layerUpdateTimes);
						removedPlanLayers.Remove(planLayer); //Still exists
						//planLayerUpdateTracker.AddLayer(planLayer);
					}
				}
				//Remove planlayers that no longer exist
				foreach (PlanLayer removedPlanLayer in removedPlanLayers)
				{
					PlanLayers.Remove(removedPlanLayer);
					removedPlanLayer.BaseLayer.RemovePlanLayerAndEntities(removedPlanLayer);
					removedPlanLayer.RemoveGameObjects();
					layersChanged = true;
				}

				//Update construction start time
				int maxConstructionTime = 0;
				foreach (PlanLayer planLayer in PlanLayers)
					if (planLayer.BaseLayer.AssemblyTime > maxConstructionTime)
						maxConstructionTime = planLayer.BaseLayer.AssemblyTime;
				ConstructionStartTime = StartTime - maxConstructionTime;

				//=================================== POLICY UPDATE =====================================
				PolicyManager.Instance.RunPlanUpdate(updatedData.policies, this, APolicyLogic.EPolicyUpdateStage.General);
			}

			LayerManager.Instance.UpdateVisibleLayersFromPlan(this);
			PlanManager.Instance.UpdatePlanInUI(this, nameOrDescriptionChanged, timeChanged, stateChanged, layersChanged, typeChanged, forceMonitorUpdate, oldStartTime, oldState, inTimelineBefore);
		}

		public void ReceiveMessage(PlanMessageObject a_message)
		{
			if (m_receivedPlanMessages.Contains(a_message.message_id))
				return;
			m_receivedPlanMessages.Add(a_message.message_id);

			PlanMessage newMessage = new PlanMessage() { message = a_message.message, team = SessionManager.Instance.GetTeamByTeamID(a_message.team_id), time = a_message.time, user_name = a_message.user_name };
			PlanMessages.Add(newMessage);
			if(OnMessageReceivedCallback != null)
			{
				OnMessageReceivedCallback.Invoke(newMessage);
			}
		}

		public PlanLayer GetPlanLayerForLayer(AbstractLayer baseLayer)
		{
			foreach (PlanLayer planLayer in PlanLayers)
				if (planLayer.BaseLayer.ID == baseLayer.ID)
					return planLayer;

			return null;
		}

		public PlanLayer GetPlanLayerForLayer(int baseLayerID)
		{
			foreach (PlanLayer planLayer in PlanLayers)
				if (planLayer.BaseLayer.ID == baseLayerID)
					return planLayer;

			return null;
		}

		public PlanLayer getPlanLayerForID(int planLayerID)
		{
			foreach (PlanLayer planLayer in PlanLayers)
				if (planLayer.ID == planLayerID)
					return planLayer;

			return null;
		}

		public void SendPlanCreation(BatchRequest a_batch)
		{
			JObject dataObject = new JObject();
			creationBatchCallID = a_batch.AddRequest<int>(Server.PostPlan(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CREATE, HandleDatabaseIDResult);
		}

		void HandleDatabaseIDResult(int a_result)
		{
			ID = a_result;
		}

		public Rect GetBounds()
		{
			if (PlanLayers.Count == 0) { return new Rect(); }
			Rect result = PlanLayers[0].GetBounds();
			for (int i = 1; i < PlanLayers.Count; ++i)
			{
				Rect planLayerRect = PlanLayers[i].GetBounds();
				if (planLayerRect.width > 0 && planLayerRect.height > 0)
				{
					if (result.width > 0 && result.height > 0)
					{
						Vector2 min = Vector2.Min(result.min, planLayerRect.min);
						Vector2 max = Vector2.Max(result.max, planLayerRect.max);
						result = new Rect(min, max - min);
					}
					else
					{
						result = planLayerRect;
					}
				}
			}

			return result;
		}
		
		public void CalculateRequiredApproval()
		{
			bool requireAMApproval = false;
			EApprovalType requiredApprovalLevel = EApprovalType.NotDependent;
			countryApproval = new Dictionary<int, EPlanApprovalState>();

			//Store this so we don't have to find removed geometry twice per layer
			List<List<SubEntity>> removedGeom = new List<List<SubEntity>>();

			//Check required approval level for layers in plan
			for (int i = 0; i < PlanLayers.Count; i++)
			{
				//Check removed geometry
				List<SubEntity> removedSubEntities = PlanLayers[i].GetInstancesOfRemovedGeometry();
				foreach (SubEntity t in removedSubEntities)
				{
					foreach (EntityType type in t.Entity.EntityTypes)
					{
						if (type.requiredApproval == EApprovalType.AreaManager)
							requireAMApproval = true;
						else if (type.requiredApproval > requiredApprovalLevel)
							requiredApprovalLevel = type.requiredApproval;
					}
				}
				//Check new geometry
				for (int entityIndex = 0; entityIndex < PlanLayers[i].GetNewGeometryCount(); ++entityIndex)
				{
					Entity t = PlanLayers[i].GetNewGeometryByIndex(entityIndex);
					foreach (EntityType type in t.EntityTypes)
					{
						if (type.requiredApproval == EApprovalType.AreaManager)
							requireAMApproval = true;
						else if (type.requiredApproval > requiredApprovalLevel)
							requiredApprovalLevel = type.requiredApproval;
					}
				}
				removedGeom.Add(removedSubEntities);
			}

			if (requireAMApproval)
				countryApproval.Add(SessionManager.AM_ID, EPlanApprovalState.Maybe);

			//Check required approval for policies
			foreach(var kvp in m_policies)
			{
				kvp.Value.logic.GetRequiredApproval(kvp.Value, this, countryApproval, ref requiredApprovalLevel);
			}

			if (requiredApprovalLevel >= EApprovalType.AllCountries)
			{
				//All team approval required, there is no chance for AM approval
				foreach (KeyValuePair<int, Team> kvp in SessionManager.Instance.GetTeamsByID())
					if (!kvp.Value.IsManager && kvp.Value.ID != SessionManager.Instance.CurrentUserTeamID)
						countryApproval.Add(kvp.Value.ID, EPlanApprovalState.Maybe);
			}
			else
			{
				//Actually check the geometry to add required approval based on level
				if (requiredApprovalLevel > 0 && LayerManager.Instance.EEZLayer != null)
				{
					List<PolygonEntity> EEZs = LayerManager.Instance.EEZLayer.Entities;
					int userCountry = SessionManager.Instance.CurrentUserTeamID;
					for (int i = 0; i < PlanLayers.Count; i++)
					{
						//The overlap function depends on the layer type
						Func<PolygonSubEntity, SubEntity, bool> overlapCheck;
						if (PlanLayers[i].BaseLayer is PolygonLayer)
							overlapCheck = (a, b) => Util.PolygonPolygonIntersection(a, b as PolygonSubEntity);
						else if (PlanLayers[i].BaseLayer is LineStringLayer)
							overlapCheck = (a, b) => Util.PolygonLineIntersection(a, b as LineStringSubEntity);
						else
							overlapCheck = (a, b) => Util.PolygonPointIntersection(a, b as PointSubEntity);

						//Check for new geometry
						for (int entityIndex = 0; entityIndex < PlanLayers[i].GetNewGeometryCount(); ++entityIndex)
						{
							Entity t = PlanLayers[i].GetNewGeometryByIndex(entityIndex);
							if (t.Country != userCountry && !countryApproval.ContainsKey(t.Country))
								countryApproval.Add(t.Country, EPlanApprovalState.Maybe);
							foreach (PolygonEntity eez in EEZs)
								if (eez.Country != userCountry && !countryApproval.ContainsKey(eez.Country) && overlapCheck(eez.GetPolygonSubEntity(), t.GetSubEntity(0)))
									countryApproval.Add(eez.Country, EPlanApprovalState.Maybe);
						}
					}
				}

				//Check for removed geometry. Only the country which owns the geometry will need to give their approval.
				for (int i = 0; i < PlanLayers.Count; i++)
				{
					foreach (SubEntity t in removedGeom[i])
					{
						if (t.Entity.Country != Entity.INVALID_COUNTRY_ID && t.Entity.Country != SessionManager.Instance.CurrentUserTeamID && !countryApproval.ContainsKey(t.Entity.Country))
						{
							countryApproval.Add(t.Entity.Country, EPlanApprovalState.Maybe);
						}
					}
				}
			}

			//Remove owner from required approval
			if (countryApproval.ContainsKey(Country))
				countryApproval.Remove(Country);
			if (countryApproval.ContainsKey(-1))
				countryApproval.Remove(-1);
			if (countryApproval.ContainsKey(SessionManager.GM_ID))
				countryApproval.Remove(SessionManager.GM_ID);
		}

		public bool NeedsApproval()
		{
			return countryApproval != null && countryApproval.Count > 0;
		}

		public bool NeedsApprovalFrom(int country)
		{
			return countryApproval != null && countryApproval.ContainsKey(country);
		}

		public bool HasApproval()
		{
			if (countryApproval == null)
				return true;
			foreach (KeyValuePair<int, EPlanApprovalState> kvp in countryApproval)
				if (kvp.Value != EPlanApprovalState.Approved)
					return false;
			return true;
		}

		public bool InInfluencingState
		{
			get { return State.IsInfluencingState(); }
		}

		public bool ShouldBeVisibleInUI
		{
			get
			{
				return (InInfluencingState && StartTime >= 0) || SessionManager.Instance.CurrentUserTeamID == Country || SessionManager.Instance.AreWeManager;
			}
		}

		public bool ShouldBeVisibleInTimeline
		{
			get
			{
				return ShouldBeVisibleInUI && State != Plan.PlanState.DELETED;
			}
		}

		public bool IsLockedByUs
		{
			get { return SessionManager.Instance.CurrentSessionID == LockedBy; }
		}

		public bool RequiresTimeChange
		{
			get { return State == PlanState.DELETED && ConstructionStartTime <= TimeManager.Instance.GetCurrentMonth(); }
		}

		public static PlanState StringToPlanState(string state)
		{
			try
			{
				return (PlanState)Enum.Parse(typeof(PlanState), state);
			}
			catch (Exception e)
			{
				Debug.LogError("Could not parse: \"" + state + "\" to a valid planstate. Exception: " + e.Message);
			}
			return PlanState.DESIGN;
		}

		public bool IsLayerpartOfPlan(AbstractLayer layer)
		{
			foreach (PlanLayer pl in PlanLayers)
				if (pl.BaseLayer.ID == layer.ID)
					return true;
			return false;
		}

		public int CompareTo(Plan other)
		{
			if (other == null)
				return 1;
			if (other.StartTime != StartTime)
				return StartTime.CompareTo(other.StartTime);
			else
				return ID.CompareTo(other.ID);
		}

		public SubEntity CheckForInvalidGeometry()
		{
			foreach (PlanLayer layer in PlanLayers)
			{
				PolygonLayer polyLayer = layer.BaseLayer as PolygonLayer;
				if (polyLayer != null)
				{
					foreach (Entity ent in layer.GetNewGeometry())
					{
						PolygonSubEntity polySubEnt = ent.GetSubEntity(0) as PolygonSubEntity;
						if (polySubEnt.InvalidPoints != null && polySubEnt.InvalidPoints.Count > 0)
							return polySubEnt;
					}
				}
			}
			return null;
		}

		public bool CheckForInvalidCables()
		{
			foreach (PlanLayer planLayer in PlanLayers)
			{
				//Check all new geometry in cable layers
				if (planLayer.BaseLayer.IsEnergyLineLayer() && planLayer.GetNewGeometryCount() > 0)
				{
					//Create layer states for energy layers of a marching color, ignoring the cable layer
					Dictionary<AbstractLayer, LayerState> energyLayerStates = new Dictionary<AbstractLayer, LayerState>();
					foreach (AbstractLayer energyLayer in PolicyLogicEnergy.Instance.energyLayers)
						if (energyLayer.greenEnergy == planLayer.BaseLayer.greenEnergy && energyLayer.ID != planLayer.BaseLayer.ID)
							energyLayerStates.Add(energyLayer, energyLayer.GetLayerStateAtPlan(this));

					foreach (Entity entity in planLayer.GetNewGeometry())
					{
						//Check the 2 connections for valid points
						EnergyLineStringSubEntity cable = (EnergyLineStringSubEntity)entity.GetSubEntity(0);
						foreach (Connection conn in cable.connections)
						{
							bool found = false;
							AbstractLayer targetPointLayer = conn.point.sourcePolygon == null ? conn.point.Entity.Layer : conn.point.sourcePolygon.Entity.Layer;
							foreach (Entity existingEntity in energyLayerStates[targetPointLayer].baseGeometry)
							{
								if (existingEntity.DatabaseID == conn.point.GetDatabaseID())
								{
									found = true;
									break;
								}
							}
							if (!found)
								return true;
						}
					}
				}
			}
			return false;
		}

		public void ZoomToPlan()
		{ 
			if(RectValid())
				CameraManager.Instance.ZoomToBounds(GetPlanRect());
		}

		public bool RectValid()
		{
			if (PlanLayers.Count == 0) return false;
			for (int i = 0; i < PlanLayers.Count; i++)
			{
				if (PlanLayers[i].RemovedGeometry.Count > 0) return true;
				if (PlanLayers[i].GetNewGeometryCount() > 0) return true;
			}
			return false;
		}

		public Rect GetPlanRect()
		{
			Vector3 min = Vector3.one * float.MaxValue;
			Vector3 max = Vector3.one * float.MinValue;

			for (int i = 0; i < PlanLayers.Count; i++)
			{
				//Check removed geometry
				List<SubEntity> removedSubEntities = PlanLayers[i].GetInstancesOfRemovedGeometry();
				foreach (SubEntity subEntity in removedSubEntities)
				{
					min = Vector3.Min(min, subEntity.BoundingBox.min);
					max = Vector3.Max(max, subEntity.BoundingBox.max);
				}
				//Check new geometry
				for (int entityIndex = 0; entityIndex < PlanLayers[i].GetNewGeometryCount(); ++entityIndex)
				{
					SubEntity subEntity = PlanLayers[i].GetNewGeometryByIndex(entityIndex).GetSubEntity(0);
					min = Vector3.Min(min, subEntity.BoundingBox.min);
					max = Vector3.Max(max, subEntity.BoundingBox.max);
				}
			}
			return new Rect(min, max - min); 
		}

		public bool TryGetPolicyData<T>(string a_policyType, out T a_result) where T : APolicyPlanData
		{
			if(m_policies.TryGetValue(a_policyType, out var temp))
			{
				a_result = (T)temp;
				return true;
			}
			a_result = null;
			return false;
		}

		public void AddPolicyData(APolicyPlanData a_data)
		{
			m_policies.Add(a_data.logic.m_definition.m_name, a_data);
		}

		

		public string GetDataBaseOrBatchIDReference()
		{
			if (ID != -1)
				return ID.ToString();
			else
				return BatchRequest.FormatCallIDReference(creationBatchCallID);
		}

		public bool HasErrors()
		{
			return GetMaximumIssueSeverity() <= ERestrictionIssueType.Error;
		}

		public ERestrictionIssueType GetMaximumIssueSeverity()
		{
			if (HasPolicyErrors())
				return ERestrictionIssueType.Error;

			ERestrictionIssueType maxSeverity = ERestrictionIssueType.None;
			foreach(PlanLayer planlayer in PlanLayers)
			{
				if (maxSeverity == ERestrictionIssueType.Error)
					break;
				if(planlayer.issues != null)
				{
					foreach(PlanIssueObject issue in planlayer.issues)
					{
						if (issue.type < maxSeverity)
						{
							maxSeverity = issue.type;
						}
					}
				}
			}
			return maxSeverity;
		}

		public List<PlanIssueObject> GetIssueList()
		{
			List<PlanIssueObject> result = new List<PlanIssueObject>(32);

			foreach (PlanLayer planlayer in PlanLayers)
			{
				if(planlayer.issues != null)
					result.AddRange(planlayer.issues);
			}
			return result;
		}

		#region ServerCommunication
		public void AttemptUnlock(bool forceUnlock, Action<string> callback = null)
		{
			NetworkForm form = new NetworkForm();
			form.AddField("id", ID);
			form.AddField("force_unlock", forceUnlock ? "1" : "0");
			form.AddField("user", SessionManager.Instance.CurrentSessionID.ToString());
			ServerCommunication.Instance.DoRequest<string>(Server.UnlockPlan(), form, callback, ServerCommunication.EWebRequestFailureResponse.Crash);
		}

		public void AttemptUnlock(BatchRequest batch)
		{
			JObject dataObject = new JObject();
			dataObject.Add("id", ID);
			dataObject.Add("force_unlock", 0);
			dataObject.Add("user", SessionManager.Instance.CurrentSessionID.ToString());
			batch.AddRequest(Server.UnlockPlan(), dataObject, BatchRequest.BATCH_GROUP_UNLOCK);
		}

		public void SubmitState(PlanState newState, BatchRequest batch)
		{
			if (newState == State)
				return;
			if (newState == PlanState.APPROVAL)
			{
				//makes sure approval is reset if we move to the approval state
				//SubmitRequiredApproval(batch); //todo: reenable
			}

			JObject dataObject = new JObject();
			dataObject.Add("id", GetDataBaseOrBatchIDReference());
			dataObject.Add("state", newState.ToString());
			dataObject.Add("user", SessionManager.Instance.CurrentSessionID.ToString());
			batch.AddRequest(Server.SetPlanState(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}

		public void SubmitDescription(BatchRequest batch)
		{
			JObject dataObject = new JObject();

			dataObject.Add("id", GetDataBaseOrBatchIDReference());
			dataObject.Add("description", string.IsNullOrEmpty(Description) ? " " : Description);

			batch.AddRequest(Server.SetPlanDescription(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}

		public void SubmitName(BatchRequest batch)
		{
			JObject dataObject = new JObject();
			dataObject.Add("id", GetDataBaseOrBatchIDReference());
			dataObject.Add("name", Name);
			batch.AddRequest(Server.RenamePlanLayer(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}

		public void SubmitPlanDate(BatchRequest batch)
		{
			JObject dataObject = new JObject();
			dataObject.Add("id", GetDataBaseOrBatchIDReference());
			dataObject.Add("date", StartTime);
			batch.AddRequest(Server.ChangePlanDate(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}

		public void SubmitAddNewPlanLayer(AbstractLayer layer, BatchRequest batch)
		{
			JObject dataObject = new JObject();
			dataObject.Add("id", GetDataBaseOrBatchIDReference());
			dataObject.Add("layerid", layer.ID);
			batch.AddRequest(Server.AddPlanLayer(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}

		public void SubmitRemovePlanLayer(PlanLayer planLayerToRemove, BatchRequest batch)
		{
			JObject dataObject = new JObject();
			dataObject.Add("id", planLayerToRemove.ID);
			batch.AddRequest(Server.DeletePlanLayer(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}

		public void SubmitRequiredApproval(BatchRequest batch)
		{
			JObject dataObject = new JObject();
			if (countryApproval.Count > 0)
			{
				List<int> countries = new List<int>(countryApproval.Count);
				foreach (KeyValuePair<int, EPlanApprovalState> kvp in countryApproval)
					countries.Add(kvp.Key);
				dataObject.Add("countries", JToken.FromObject(countries));
			}
			dataObject.Add("id", GetDataBaseOrBatchIDReference());
			batch.AddRequest(Server.AddApproval(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}

		public void AddSystemMessage(string text)
		{
			NetworkForm form = new NetworkForm();
			form.AddField("plan", GetDataBaseOrBatchIDReference());
			form.AddField("team_id", SessionManager.GM_ID);
			form.AddField("user_name", "[SYSTEM]");
			form.AddField("text", text);

			ServerCommunication.Instance.DoRequest(Server.PostPlanFeedback(), form);
		}

		public void SendMessage(string text)
		{
			NetworkForm form = new NetworkForm();
			form.AddField("plan", GetDataBaseOrBatchIDReference());
			form.AddField("team_id", SessionManager.Instance.CurrentUserTeamID);
			form.AddField("user_name", SessionManager.Instance.CurrentUserName);
			form.AddField("text", text);
			ServerCommunication.Instance.DoRequest(Server.PostPlanFeedback(), form);
		}

		public void SendMessage(string text, BatchRequest batch)
		{
			JObject dataObject = new JObject();
			dataObject.Add("plan", GetDataBaseOrBatchIDReference());
			dataObject.Add("team_id", SessionManager.Instance.CurrentUserTeamID);
			dataObject.Add("user_name", SessionManager.Instance.CurrentUserName);
			dataObject.Add("text", text);
			batch.AddRequest(Server.PostPlanFeedback(), dataObject, BatchRequest.BATCH_GROUP_PLAN_CHANGE);
		}
		#endregion
	}

	public class PlanLayerUpdateTracker
	{
		List<PlanLayer> remainingLayers;

		public PlanLayerUpdateTracker()
		{
			remainingLayers = new List<PlanLayer>();
		}

		public void AddLayer(PlanLayer layer)
		{
			remainingLayers.Add(layer);
		}
	}
}