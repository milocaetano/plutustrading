using System;

namespace DojoRelatorio
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using ClosedXML.Excel;

    class Program
    {
        static async Task Main(string[] args)
        {


            string exeFolderPath = @"C:\Users\Camilo\Documents\OLIVER2\"; // Obtém a pasta do executável

            if (!Directory.Exists(exeFolderPath))
            {
                Console.WriteLine("Nenhum arquivo CSV encontrado na pasta do programa.");
                Console.ReadKey();

            }
            string[] csvFiles = Directory.GetFiles(exeFolderPath, "*merged*.csv"); // Busca por arquivos CSV na pasta

            if (csvFiles.Length == 0)
            {
                // Não foram encontrados arquivos CSV
                Console.WriteLine("Nenhum arquivo CSV encontrado na pasta do programa.");
                Console.ReadKey();

            }

            string csvFilePath = csvFiles[0]; // Pega o primeiro arquivo CSV encontrado
            string excelFilePath = Path.ChangeExtension(csvFilePath, ".xlsx"); // Cria o caminho para o arquivo Excel com o mesmo nome

            var cache = new Dictionary<string, decimal>(); // Cache para armazenar variações

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Dados");
                string[] lines = File.ReadAllLines(csvFilePath);
                var cultureInfo = new CultureInfo("pt-BR");

                for (int i = 5; i < lines.Length; i++) // Começa na linha 6
                {
                    try
                    {
                        string[] columns = lines[i].Split(';');
                        string[] dateTime = columns[1].Split(' ');
                        string[] closingDateTime = columns[2].Split(' ');

                        // Colunas A e B
                        DateTime entradaDate = DateTime.ParseExact(dateTime[0], "dd/MM/yyyy", cultureInfo);
                        worksheet.Cell(i - 4, "A").Value = entradaDate;
                        worksheet.Cell(i - 4, "B").Value = DateTime.ParseExact(dateTime[1], "HH:mm:ss", cultureInfo).TimeOfDay;

                        // Colunas C e D
                        worksheet.Cell(i - 4, "C").Value = "Daytrade";
                        string ativo = columns[0].StartsWith("WIN") ? "WINFUT" : (columns[0].StartsWith("WDO") ? "WDOFUT" : "");
                        worksheet.Cell(i - 4, "D").Value = ativo;

                        // Coluna E
                        worksheet.Cell(i - 4, "E").Value = columns[6];

                        // Coluna F
                        worksheet.Cell(i - 4, "F").Value = columns[5];

                        // Coluna G
                        decimal price = 0;
                        if (columns[6] == "C")
                        {
                            price = Convert.ToDecimal(columns[7], cultureInfo);
                            worksheet.Cell(i - 4, "G").Value = price;
                        }
                        else if (columns[6] == "V")
                        {
                            price = Convert.ToDecimal(columns[8], cultureInfo);
                            worksheet.Cell(i - 4, "G").Value = price;
                        }

                        // Coluna H
                        decimal adjustment = worksheet.Cell(i - 4, "D").Value.ToString() == "WDOFUT" ? 7 : 100;
                        worksheet.Cell(i - 4, "H").Value = columns[6] == "C" ? price - adjustment : price + adjustment;

                        // Coluna J
                        decimal doubleAdjustment = adjustment * 2;
                        worksheet.Cell(i - 4, "J").Value = columns[6] == "C" ? price + doubleAdjustment : price - doubleAdjustment;

                        // Colunas M e N
                        worksheet.Cell(i - 4, "M").Value = DateTime.ParseExact(closingDateTime[0], "dd/MM/yyyy", cultureInfo);
                        worksheet.Cell(i - 4, "N").Value = DateTime.ParseExact(closingDateTime[1], "HH:mm:ss", cultureInfo).TimeOfDay;

                        // Coluna O
                        if (columns[6] == "C")
                        {
                            worksheet.Cell(i - 4, "O").Value = columns[8];
                        }
                        else if (columns[6] == "V")
                        {
                            worksheet.Cell(i - 4, "O").Value = columns[7];
                        }
                    }catch(Exception ex)
                    {
                        
                        continue;
                    }
                }


                // Formatar as colunas para data e hora
                worksheet.Column("A").Style.DateFormat.Format = "dd/MM/yyyy";
                worksheet.Column("B").Style.DateFormat.Format = "HH:mm:ss";
                worksheet.Column("M").Style.DateFormat.Format = "dd/MM/yyyy";
                worksheet.Column("N").Style.DateFormat.Format = "HH:mm:ss";

                // Formatar as colunas de preço como número
                worksheet.Columns("G").Style.NumberFormat.Format = "#,##0.00";
                worksheet.Columns("H").Style.NumberFormat.Format = "#,##0.00";
                worksheet.Columns("J").Style.NumberFormat.Format = "#,##0.00";
                worksheet.Columns("O").Style.NumberFormat.Format = "#,##0.00";


                var allCells = worksheet.RangeUsed().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                allCells.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

                // Salvar o arquivo Excel
                workbook.SaveAs(excelFilePath);



                Console.WriteLine("Arquivo Excel criado com sucesso! Olha na pasta c:\relatorios");
                Console.ReadKey();
            }
        }
        public static string GetAssetCode(string assetSymbol)
        {
            switch (assetSymbol)
            {
                case "WINFUT":
                    return "WINFUT.SA"; // Substitua por código correto, se necessário
                case "WDOFUT":
                    return "WDOFUT.SA"; // Substitua por código correto, se necessário
                default:
                    return "";
            }
        }
    }

}
