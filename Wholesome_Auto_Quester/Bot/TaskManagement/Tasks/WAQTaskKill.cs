﻿using Wholesome_Auto_Quester.Database.Models;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.Bot.TaskManagement.Tasks
{
    public class WAQTaskKill : WAQBaseScannableTask
    {
        public WAQTaskKill(ModelQuestTemplate questTemplate, ModelCreatureTemplate creatureTemplate, ModelCreature creature)
            : base(creature.GetSpawnPosition, creature.map, $"Kill {creatureTemplate.name} for {questTemplate.LogTitle}", creatureTemplate.entry, creature.spawnTimeSecs)
        {
            if (questTemplate.QuestAddon?.AllowableClasses > 0)
            {
                PriorityShift = 3;
            }
            if (questTemplate.TimeAllowed > 0)
            {
                PriorityShift = 7;
            }
        }

        public new void PutTaskOnTimeout(string reason, int timeInSeconds, bool exponentiallyLonger)
            => base.PutTaskOnTimeout(reason, timeInSeconds > 0 ? timeInSeconds : DefaultTimeOutDuration, exponentiallyLonger);

        public override bool IsObjectValidForTask(WoWObject wowObject)
        {
            if (wowObject is WoWUnit unit)
            {
                return unit.IsAlive && unit.IsAttackable;
            }
            return false;
        }

        public override void PostInteraction(WoWObject wowObject)
        {
            WoWUnit killTarget = (WoWUnit)wowObject;
            if (killTarget.IsDead && Location.DistanceTo(killTarget.Position) < 20)
            {
                PutTaskOnTimeout("Completed");
                return;
            }
        }

        public override string TrackerColor => IsTimedOut ? "Gray" : "OrangeRed";
        public override TaskInteraction InteractionType => TaskInteraction.Kill;
    }
}