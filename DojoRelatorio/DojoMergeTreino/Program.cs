using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        string directoryPath = @"C:\Users\Camilo\Documents\OLIVER2"; // Substitua pelo caminho correto da pasta
        string outputPath = @"C:\Users\Camilo\Documents\OLIVER2\merged_oliver.csv"; // Substitua pelo caminho desejado para o arquivo de saída

        // Obtém todos os arquivos CSV no diretório
        var fileInfo = new DirectoryInfo(directoryPath).GetFiles("*.csv").OrderBy(f => f.CreationTime).ToList();

        List<string> filteredLines = new List<string>();

        foreach (var file in fileInfo)
        {
            // Lê todas as linhas do arquivo
            var lines = File.ReadAllLines(file.FullName);

            // Filtra as linhas que começam com "[R]"
            filteredLines.AddRange(lines.Where(line => line.StartsWith("[R]")));
        }

        // Escreve as linhas filtradas no arquivo de saída
        File.WriteAllLines(outputPath, filteredLines);

        Console.WriteLine("Arquivo filtrado criado com sucesso em: " + outputPath);
    }
}
