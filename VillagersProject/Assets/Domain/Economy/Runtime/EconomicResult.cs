using System;
using System.Collections.Generic;

[Serializable]
public class EconomicResult
{
    public bool success;
    public string message;

    public ResourceBundle consumed = new();
    public ResourceBundle produced = new();

    public List<ResourceStack> categorySpendSelection = new();

    public static EconomicResult Ok(string message = "")
    {
        return new EconomicResult
        {
            success = true,
            message = message ?? string.Empty
        };
    }

    public static EconomicResult Fail(string message)
    {
        return new EconomicResult
        {
            success = false,
            message = message ?? "Economic operation failed."
        };
    }
}