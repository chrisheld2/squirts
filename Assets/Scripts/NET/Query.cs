using System.Threading.Tasks;
using UnityEngine;

public class Query : MonoBehaviour
{
    #region Constants
    private const string API_ENDPOINT = "https://136.53.31.9:5502/api/info";
    #endregion

    #region Events
    public event System.Func<string, Task> OnResults;
    #endregion

    #region Public Methods
    public async Task Send(string message)
    {
        try
        {
            string results = await HTTPREST.Request(API_ENDPOINT);

            if (OnResults != null)
                await OnResults.Invoke(results);

            DL.Log("Query.Send: " + results);
        }
        catch (System.Exception ex)
        {
            DL.Log($"Query.Send Error: {ex.Message}", "red");
        }
    }
    #endregion
}
