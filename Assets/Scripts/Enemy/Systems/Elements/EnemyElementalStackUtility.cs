using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Shared helper that applies elemental stacks and threshold procs on enemies.
/// </summary>
public static class EnemyElementalStackUtility
{
    #region Methods

    #region Public Methods
    public static bool TryApplyStacks(Entity enemyEntity,
                                      float stacksToAdd,
                                      in ElementalEffectConfig effectConfig,
                                      ref BufferLookup<EnemyElementStackElement> stackLookup,
                                      out bool thresholdProcTriggered)
    {
        thresholdProcTriggered = false;

        if (stacksToAdd <= 0f)
            return false;

        if (stackLookup.HasBuffer(enemyEntity) == false)
            return false;

        DynamicBuffer<EnemyElementStackElement> stackBuffer = stackLookup[enemyEntity];
        int stackIndex = FindStackIndex(in stackBuffer, effectConfig.ElementType);
        EnemyElementStackElement stackElement = stackIndex >= 0 ? stackBuffer[stackIndex] : BuildInitialStack(in effectConfig);

        SynchronizeStackDefinition(ref stackElement, in effectConfig);

        float previousStacks = math.max(0f, stackElement.CurrentStacks);
        float maximumStacks = math.max(0.1f, stackElement.MaximumStacks);
        float thresholdStacks = math.max(0.1f, stackElement.ProcThresholdStacks);
        float nextStacks = previousStacks + stacksToAdd;

        if (nextStacks > maximumStacks)
            nextStacks = maximumStacks;

        stackElement.CurrentStacks = nextStacks;
        bool crossedThreshold = previousStacks < thresholdStacks && nextStacks >= thresholdStacks;

        if (crossedThreshold)
        {
            thresholdProcTriggered = true;
            TriggerProc(ref stackElement);

            if (stackElement.ConsumeStacksOnProc != 0)
                stackElement.CurrentStacks = math.max(0f, stackElement.CurrentStacks - thresholdStacks);
        }

        if (stackIndex >= 0)
            stackBuffer[stackIndex] = stackElement;
        else
            stackBuffer.Add(stackElement);

        return true;
    }
    #endregion

    #region Helpers
    private static int FindStackIndex(in DynamicBuffer<EnemyElementStackElement> stackBuffer, ElementType elementType)
    {
        for (int index = 0; index < stackBuffer.Length; index++)
        {
            if (stackBuffer[index].ElementType == elementType)
                return index;
        }

        return -1;
    }

    private static EnemyElementStackElement BuildInitialStack(in ElementalEffectConfig effectConfig)
    {
        EnemyElementStackElement stackElement = new EnemyElementStackElement
        {
            ElementType = effectConfig.ElementType,
            EffectKind = effectConfig.EffectKind,
            ProcMode = effectConfig.ProcMode,
            CurrentStacks = 0f,
            DotRemainingSeconds = 0f,
            DotTickTimer = 0f,
            ImpedimentRemainingSeconds = 0f,
            CurrentImpedimentSlowPercent = 0f
        };

        SynchronizeStackDefinition(ref stackElement, in effectConfig);
        return stackElement;
    }

    private static void SynchronizeStackDefinition(ref EnemyElementStackElement stackElement, in ElementalEffectConfig effectConfig)
    {
        float maximumStacks = math.max(0.1f, effectConfig.MaximumStacks);
        float procThresholdStacks = math.max(0.1f, effectConfig.ProcThresholdStacks);

        if (procThresholdStacks > maximumStacks)
            procThresholdStacks = maximumStacks;

        stackElement.ElementType = effectConfig.ElementType;
        stackElement.EffectKind = effectConfig.EffectKind;
        stackElement.ProcMode = effectConfig.ProcMode;
        stackElement.MaximumStacks = maximumStacks;
        stackElement.ProcThresholdStacks = procThresholdStacks;
        stackElement.StackDecayPerSecond = math.max(0f, effectConfig.StackDecayPerSecond);
        stackElement.ConsumeStacksOnProc = effectConfig.ConsumeStacksOnProc;
        stackElement.DotDamagePerTick = math.max(0f, effectConfig.DotDamagePerTick);
        stackElement.DotTickInterval = math.max(0.01f, effectConfig.DotTickInterval);
        stackElement.DotDurationSeconds = math.max(0.05f, effectConfig.DotDurationSeconds);
        stackElement.ImpedimentSlowPercentPerStack = math.clamp(effectConfig.ImpedimentSlowPercentPerStack, 0f, 100f);
        stackElement.ImpedimentProcSlowPercent = math.clamp(effectConfig.ImpedimentProcSlowPercent, 0f, 100f);
        stackElement.ImpedimentMaxSlowPercent = math.clamp(effectConfig.ImpedimentMaxSlowPercent, 0f, 100f);
        stackElement.ImpedimentDurationSeconds = math.max(0.05f, effectConfig.ImpedimentDurationSeconds);
    }

    private static void TriggerProc(ref EnemyElementStackElement stackElement)
    {
        switch (stackElement.EffectKind)
        {
            case ElementalEffectKind.Dots:
                stackElement.DotRemainingSeconds = math.max(stackElement.DotRemainingSeconds, stackElement.DotDurationSeconds);
                stackElement.DotTickTimer = math.max(0.01f, stackElement.DotTickInterval);
                return;
            case ElementalEffectKind.Impediment:
                float procSlowPercent = math.min(stackElement.ImpedimentProcSlowPercent, stackElement.ImpedimentMaxSlowPercent);

                if (procSlowPercent < 0f)
                    procSlowPercent = 0f;

                stackElement.CurrentImpedimentSlowPercent = procSlowPercent;
                stackElement.ImpedimentRemainingSeconds = math.max(stackElement.ImpedimentRemainingSeconds, stackElement.ImpedimentDurationSeconds);
                return;
        }
    }
    #endregion

    #endregion
}
