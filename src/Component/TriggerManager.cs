using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SimpleJSON;

namespace AudioMate
{
    public class TriggerManager
    {
        private readonly AudioMateController _controller;

        public readonly List<string> ColliderChoices = new List<string>();
        private readonly Dictionary<string,TriggerActionDiscrete> _startTriggers = new Dictionary<string,TriggerActionDiscrete>();
        private readonly Dictionary<string,TriggerActionDiscrete> _endTriggers = new Dictionary<string,TriggerActionDiscrete>();

        public const string DefaultCollider = "VaginaTrigger";
        public const string StartTriggerAction = "StartTriggerAction";
        public const string EndTriggerAction = "EndTriggerAction";

        public TriggerManager(AudioMateController controller)
        {
            _controller = controller;
            Init();
        }

        private void Init()
        {
            ColliderChoices.Add("LipTrigger");
            ColliderChoices.Add("MouthTrigger");
            ColliderChoices.Add("ThroatTrigger");
            ColliderChoices.Add("lNippleTrigger");
            ColliderChoices.Add("rNippleTrigger");
            ColliderChoices.Add("LabiaTrigger");
            ColliderChoices.Add("VaginaTrigger");
            ColliderChoices.Add("DeepVaginaTrigger");
            ColliderChoices.Add("DeeperVaginaTrigger");
        }

        private static JSONStorable GetPluginStorableById(Atom atom, string id)
        {
            var storableIdName = atom.GetStorableIDs().FirstOrDefault((string storeId) => !string.IsNullOrEmpty(storeId) && storeId.Contains(id));
            return storableIdName == null ? null : atom.GetStorableByID(storableIdName);
        }

        private void CleanUp()
        {
            foreach (var triggerAction in _startTriggers.Where(triggerAction => triggerAction.Value == null))
            {
                _startTriggers.Remove(triggerAction.Key);
                Log($"Removed empty start trigger action {triggerAction.Key}");
            }
            foreach (var triggerAction in _endTriggers.Where(triggerAction => triggerAction.Value == null))
            {
                _endTriggers.Remove(triggerAction.Key);
                Log($"Removed empty end trigger action {triggerAction.Key}");
            }
        }

        private static bool DoesTriggerActionExist(CollisionTrigger trigger, string triggerActionName, string triggerActionType = StartTriggerAction)
        {
            JSONNode presentTriggers = trigger.trigger.GetJSON();
            var asArray = triggerActionType == StartTriggerAction ? presentTriggers["startActions"].AsArray : presentTriggers["endActions"].AsArray;
            for (var i = 0; i < asArray.Count; i++) {
                var asObject = asArray[i].AsObject;
                string name = asObject["name"];
                if (name == triggerActionName && asObject["receiver"] != null){
                    return true;
                }
            }
            return false;
        }

        /*private TriggerActionDiscrete GetTriggerAction(CollisionTrigger trigger, string triggerActionName, string triggerActionType = StartTriggerAction)
        {
            JSONNode presentTriggers = trigger.trigger.GetJSON();
            var asArray = triggerActionType == StartTriggerAction ? presentTriggers["startActions"].AsArray : presentTriggers["endActions"].AsArray;
            for (var i = 0; i < asArray.Count; i++) {
                var asObject = asArray[i].AsObject;
                string name = asObject["name"];
                if (name == triggerActionName && asObject["receiver"] != null)
                {
                    var triggerAction = _controller.containingAtom.get) as TriggerActionDiscrete;
                }
            }
            return false;
        }*/

        public TriggerActionDiscrete AddTriggerAction(string triggerID, string triggerActionName, string receiverAtomID, string receiverNodeID, string triggerActionType = StartTriggerAction)
        {
            CleanUp();
            var trigger = _controller.containingAtom.GetStorableByID(triggerID) as CollisionTrigger;
            if ((UnityEngine.Object) trigger == (UnityEngine.Object) null) return null;
            if (DoesTriggerActionExist(trigger, triggerActionName, triggerActionType))
            {
                if (triggerActionType == StartTriggerAction)
                {
                    if (_startTriggers.ContainsKey(triggerActionName)) return null;
                }
                else
                {
                    if (_endTriggers.ContainsKey(triggerActionName)) return null;
                }
            }
            var newTriggerAction = triggerActionType == StartTriggerAction
                ? trigger.trigger.CreateDiscreteActionStartInternal()
                : trigger.trigger.CreateDiscreteActionEndInternal();
            newTriggerAction.name = triggerActionName;
            newTriggerAction.receiverAtom = _controller.containingAtom;
            newTriggerAction.receiver = GetPluginStorableById(_controller.GetContainingAtom(), receiverAtomID);
            newTriggerAction.receiverTargetName = receiverNodeID;
            trigger.enabled = true;
            if (triggerActionType == StartTriggerAction)
            {
                _startTriggers.Add(triggerActionName, newTriggerAction);
            }
            else
            {
                _endTriggers.Add(triggerActionName, newTriggerAction);
            }

            return newTriggerAction;
        }

        public void RemoveTriggerAction(string triggerID, string triggerActionName)
        {

            var trigger = _controller.containingAtom.GetStorableByID(triggerID) as CollisionTrigger;
            if (trigger == null) return;
            if (!DoesTriggerActionExist(trigger, triggerActionName)) return;

            // TODO Add remove functionality for trigger actions
        }

        public void UpdateTriggerAction(string oldTriggerActionID, string newTriggerActionID, string newTriggerReceiverName, string triggerType = StartTriggerAction)
        {
            TriggerActionDiscrete triggerAction;
            if (triggerType == StartTriggerAction)
            {
                if (!_startTriggers.TryGetValue(oldTriggerActionID, out triggerAction)) return;
            }
            else
            {
                if (!_endTriggers.TryGetValue(oldTriggerActionID, out triggerAction)) return;
            }

            if (triggerAction == null) return;

            triggerAction.name = newTriggerActionID;
            triggerAction.SetReceiverTargetName(newTriggerReceiverName);
        }

        public void OnDestroy()
        {
            // TODO check if there's anything to clean up
        }

        public void OnAtomRename(string oldID, string newID)
        {
            // Nothing to see here...
        }

        private void Log(string message)
        {
            if ((UnityEngine.Object) _controller == (UnityEngine.Object) null) return;
            _controller.Log($"TriggerManager: {message}");
        }
    }
}
