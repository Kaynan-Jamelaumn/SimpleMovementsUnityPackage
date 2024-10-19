using System;
using System.Collections.Generic;

public class MobMovementContext
{
    private MobActionsController actionsController;

    public MobMovementContext(MobActionsController actionsController) {
        this.actionsController = actionsController;

    }
    public MobActionsController ActionsController => actionsController;

}

