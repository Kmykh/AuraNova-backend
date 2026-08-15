using System;
using System.Net.Http;
using System.Net.Http.Headers;
class Program {
    static void Main() {
        try {
            var client = new HttpClient();
            string key = null;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            client.DefaultRequestHeaders.Add("apikey", key);
            Console.WriteLine("Success");
        } catch (Exception ex) {
            Console.WriteLine(ex.GetType().Name);
        }
    }
}
