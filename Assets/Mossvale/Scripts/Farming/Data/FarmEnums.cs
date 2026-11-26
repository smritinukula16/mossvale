namespace Mossvale.Farming
{
    /// <summary>What an item is used for.</summary>
    public enum ItemCategory { Tool, Seed, Produce }

    /// <summary>Tools the player can hold. Order = toolbar order (keys 1-4).</summary>
    public enum ToolType { None = -1, Hoe = 0, Seeds = 1, WateringCan = 2, Basket = 3 }

    /// <summary>Life cycle of a single farm plot.</summary>
    public enum PlotState
    {
        Untilled,   // empty grass/soil with weeds
        Tilled,     // furrow ready for seeds
        Planted,    // seed in the ground, soil dry - needs water
        Watered,    // soil wet, crop is growing towards the next stage
        Growing,    // crop grew a stage and the soil dried - needs water again
        Grown,      // fully grown, ready to harvest
        Harvested   // crop collected, soil depleted - till again to reuse
    }

    /// <summary>Visual growth stage of a crop.</summary>
    public enum CropStage { Seed = 0, Small = 1, Medium = 2, Full = 3 }

    /// <summary>Kind of feedback message, used for colour and effects.</summary>
    public enum FeedbackKind { Success, Failure, Info, Growth, Harvest, Star }
}
