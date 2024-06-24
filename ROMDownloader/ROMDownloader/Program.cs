using System;
using System.Collections.Generic;
using System.Net.Http;
using HtmlAgilityPack;

class Program
{
    private static async Task Main(string[] args)
    {
        string url = "https://archive.org/download/mame-merged/mame-merged/";
        List<string> enlaces = await GetLinksAsync(url);

        foreach (string enlace in enlaces)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    byte[] filecontents = await client.GetByteArrayAsync(enlace);
                    string fileName = Path.GetFileName(enlace);
                    await File.WriteAllBytesAsync(fileName, filecontents);
                    Console.WriteLine($"Archivo descargado: {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al descargar el archivo({enlace}) : {ex.Message}");
                }
            }
        }
    }

    public static async Task<List<string>> GetLinksAsync(string url)
    {
        List<string> enlaces = new List<string>();

        // Crea una instancia de HttpClient para realizar la solicitud HTTP
        using (HttpClient client = new HttpClient())
        {
            try
            {
                // Realiza la solicitud HTTP y obtiene el contenido de la página
                string contenidoHtml = await client.GetStringAsync(url);

                // Crea una instancia de HtmlDocument y carga el contenido HTML
                HtmlDocument documento = new HtmlDocument();
                documento.LoadHtml(contenidoHtml);

                // Selecciona todos los nodos <a> que contienen el atributo href
                foreach (HtmlNode nodo in documento.DocumentNode.SelectNodes("//a[@href]"))
                {
                    // Obtiene el valor del atributo href
                    string href = nodo.GetAttributeValue("href", string.Empty);
                    if (href.EndsWith(".zip"))
                    {
                        if (!href.StartsWith("http://") && !href.StartsWith("https://"))
                        {
                            href = url + href;
                        }
                        enlaces.Add(href);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener los enlaces: {ex.Message}");
            }
        }

        return enlaces;
    }
}
