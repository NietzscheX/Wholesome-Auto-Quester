﻿using robotManager.FiniteStateMachine;
using FlXProfiles;
using Wholesome_Auto_Quester.Bot;
using Wholesome_Auto_Quester.Helpers;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.States
{
    class WAQGoTo : State
    {
        public override string DisplayName { get; set; } = "Go To [SmoothMove - Q]";

        public override bool NeedToRun
        {
            get
            {
                if (!Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause
                    || !ObjectManager.Me.IsValid)
                    return false;

                if (WAQTasks.TaskInProgress?.TaskType == TaskType.Explore)
                {
                    DisplayName = $"Explore {WAQTasks.TaskInProgress.Location} for {WAQTasks.TaskInProgress.Quest.LogTitle} [SmoothMove - Q]";
                    return true;
                }

                return false;
            }
        }

        public override void Run()
        {
            WAQTask task = WAQTasks.TaskInProgress;
            //Logger.Log($"******** RUNNING EXPLORATION TASK {ToolBox.GetTaskId(task)}  ********");

            if (task.GetDistance < 2f) {
                MoveHelper.StopAllMove();
                Logger.Log($"Reached exploration hotspot for {task.Quest.LogTitle}");
                return;
            }
            
            if (!MoveHelper.IsMovementThreadRunning ||
                MoveHelper.CurrentMovementTarget.DistanceTo(task.Location) > 8) {
                Logger.Log($"Moving to Hotspot for {task.Quest.LogTitle} (Explore).");
                MoveHelper.StartGoToThread(task.Location, precise: true);
            }
        }
    }
}