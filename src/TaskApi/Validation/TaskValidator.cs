namespace TaskApi.Validation;

public static class TaskValidator
{
    public static string? ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title is required.";
        }

        if (title.Trim().Length > 200)
        {
            return "Title must be 200 characters or fewer.";
        }

        return null;
    }
}
