namespace UTowny.Commands;
public static class PlotMessages
{
    private static readonly Dictionary<string,string> Defaults=new()
    {
        ["plan_draft_buy"]="This plot is a draft and cannot be bought until the mayor publishes it.",
        ["plan_owned"]="Owned plots cannot be changed through planning. Their owner must release them first.",
        ["plan_not_draft"]="Unpublish this plot with /plot unpublish <name> before editing its layout or planned price.",
        ["plan_publish"]="Plot published and now for sale.",
        ["plan_unpublish"]="Plot returned to draft. It is no longer available to buyers.",
        ["plan_rename"]="Draft renamed.",
        ["plan_move"]="Draft moved. Planning outlines will update.",
        ["plan_resize"]="Draft resized to your selected corners.",
        ["plan_price"]="Planned price saved. The draft is not for sale.",
        ["plan_delete"]="Plot removed; the town claim is unchanged.",
        ["rect_invalid"]="Select two different corners forming a rectangle from 1 to 1024 metres per side, covering at most 256 claim cells.",
        ["rect_outside"]="Every part of the plot must be inside your town's claimed land on this map.",
        ["rect_legacy_overlap"]="This selection overlaps a whole-cell plot already owned or listed for sale. Unlist an unsold cell before subdividing it; owned cells cannot be subdivided.",
        ["rect_overlap"]="This selection overlaps an existing rectangular plot. Adjacent edges are allowed.",
        ["rect_name_invalid"]="Use a plot name with 1-32 letters, numbers, underscores or hyphens.",
        ["rect_name_taken"]="Your town already has a plot with that name.",
        ["rect_bank_full"]="The town treasury cannot receive the price. No money or ownership changed.",
        ["rect_bought"]="Plot purchased. Your protection covers its full horizontal rectangle at every height.",
        ["rect_listed"]="This rectangular plot is now for sale.",
        ["rect_unlisted"]="This rectangular plot is no longer for sale.",
        ["rect_permissions"]="This rectangular plot's public permissions were updated.",
        ["rect_not_owner"]="Only this plot's owner can release it.",
        ["rect_owned_delete"]="This plot is owned. Its owner must use /plot release confirm before it can be deleted.",
        ["rect_released"]="Plot returned to the town without a refund. It is not for sale.",
        ["rect_deleted"]="Rectangular plot removed. This land remains claimed by the town.",
        ["rect_cell_in_use"]="This cell contains rectangular plots. Stand inside a plot to manage it. Remove its plots before unclaiming or selling the whole cell.",
        ["rect_selection_missing"]="Select both corners on this map with /plot pos1 and /plot pos2 first. Selections expire after 30 minutes or disconnect.",
        ["rect_selection_cleared"]="Plot selection cleared.",
        ["rect_legacy_only"]="That action applies to rectangular plots. This location has no rectangular plot."
    };
    public static string Get(string key)=>Defaults.TryGetValue(key,out var text)?text:key;
}
