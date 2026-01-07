using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class HTTPREST
{
    public async static Task<string> Request(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            DL.Log("URL is null", "red");
            return null;
        }

        using (var httpClient = new System.Net.Http.HttpClient())
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.certificateHandler = new BypassCertificate();

                // Send the request and wait for a response
                await request.SendWebRequest();

                // Check for errors
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    DL.Log("Error: " + request.error, "red");
                }
                else
                {

                    return request.downloadHandler.text;

                }
            }
            return null;
        }
    }

    private class BypassCertificate : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true; // Always accept
        }
    }

}
