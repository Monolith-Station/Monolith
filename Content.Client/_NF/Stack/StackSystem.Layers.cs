using Content.Shared.Stacks.Components;
using Content.Shared.Stacks;

namespace Content.Client.Stack
{
    /// <summary>
    /// Data used to determine which layers of a stack's sprite are visible.
    /// </summary>
    public struct StackLayerData
    {
        public int Actual;
        public int MaxCount;
        public bool Hidden;
    }

    public sealed partial class StackSystem : SharedStackSystem
    {

        /// <summary>
        /// Adjusts the actual and maxCount to change how stack amounts are displayed.
        /// </summary>
        /// <param name="ent">The entity considered.</param>
        /// <param name="actual">The actual number of items in the stack. Altered depending on the function to run.</param>
        /// <param name="maxCount">The maximum number of items in the stack. Altered depending on the function to run.</param>
        /// <returns>Whether or not a function was applied.</returns>
        private bool ApplyLayerFunction(Entity<StackComponent> ent, ref int actual, ref int maxCount)
        {
            switch (ent.Comp.LayerFunction)
            {
                case StackLayerFunction.Threshold:
                    if (TryComp<StackLayerThresholdComponent>(ent, out var threshold))
                    {
                        ApplyThreshold(threshold, ref actual, ref maxCount);
                        return true;
                    }
                    break;
            }
            // No function applied.
            return false;
        }

        /// <summary>
        /// Selects which layer a stack applies based on a list of thresholds.
        /// Each threshold passed results in the next layer being selected.
        /// </summary>
        /// <param name="comp">The threshold parameters to apply.</param>
        /// <param name="actual">The number of items in the stack. Will be set to the index of the layer to use.</param>
        /// <param name="maxCount">The maximum possible number of items in the stack. Will be set to the number of selectable layers.</param>
        private static void ApplyThreshold(StackLayerThresholdComponent comp, ref int actual, ref int maxCount)
        {
            // We must stop before we run out of thresholds or layers, whichever's smaller.
            maxCount = Math.Min(comp.Thresholds.Count + 1, maxCount);
            var newActual = 0;
            foreach (var threshold in comp.Thresholds)
            {
                //If our value exceeds threshold, the next layer should be displayed.
                //Note: we must ensure actual <= MaxCount.
                if (actual >= threshold && newActual < maxCount)
                    newActual++;
                else
                    break;
            }
            actual = newActual;
        }
    }
}
