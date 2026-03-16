public static class TaskBundleMapper
{
    public static ResourceBundle BuildLegacyWorkOutputBundle(TaskInstance task)
    {
        var bundle = new ResourceBundle();

        if (task == null)
            return bundle;

        if (!string.IsNullOrWhiteSpace(task.resourceId) && task.baseAmount > 0)
            bundle.AddExact(task.resourceId, task.baseAmount);

        bundle.Normalize();
        return bundle;
    }

    public static ResourceBundle BuildLegacyTaskRewardBundle(TaskInstance task)
    {
        var bundle = new ResourceBundle();

        if (task == null)
            return bundle;

        if (task.wageGold > 0)
            bundle.AddExact("gold", task.wageGold);

        bundle.Normalize();
        return bundle;
    }

    public static ResourceBundle BuildLegacyTaskCostBundle(TaskInstance task)
    {
        // PATCH 9: optional now, empty by default.
        return new ResourceBundle();
    }
}