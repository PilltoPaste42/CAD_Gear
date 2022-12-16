namespace CGPlugin.Services.Interfaces;

using Inventor;

/// <summary>
///     Сервис для подключения к API Autodesk Inventor
/// </summary>
public class InventorWrapper : ICADApiService
{
    public InventorWrapper()
    {
        try
        {
            var applicationType = Type.GetTypeFromProgID("Inventor.Application");

            App = (Application)Activator.CreateInstance(applicationType)!;
        }
        catch (Exception)
        {
            throw new ApplicationException(@"Error: Failed to start Autodesk Inventor.");
        }

        App.Visible = true;
    }

    public Application App { get; }
}