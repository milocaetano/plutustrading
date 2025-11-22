using System;

namespace DojoRelatorioSimulado
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using ClosedXML.Excel;



    class Program
    {
        static void Main()
        {
            var estrategia = new Estrategia
            {
                StopLoss = 485,
                Parcial1 = 205,
                Parcial2 = 545
            };


            string entradasPath = "C:\\Relatorios\\entradas.csv";
            string historicoPath = "C:\\Relatorios\\WINFUT_F_0_1min.csv";           

            string resultadoPath = $"C:\\Relatorios\\resultado_estrategia{estrategia.StopLoss}_{estrategia.Parcial1}_{estrategia.Parcial2}.xlsx";

            var entradas = LerEntradas(entradasPath);
            var historico = LerHistoricoProfit(historicoPath);

           
            Console.WriteLine($"Testando Estratégia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}\n");
            var resultados = TestarEstrategia(estrategia, entradas, historico);

            GerarRelatorioExcel(resultadoPath, resultados);
            Console.WriteLine("Backtest concluído. Resultados salvos em resultado.xlsx");
        }

        static void GerarRelatorioExcel(string path, List<ResultadoTrade> resultados)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Resultados");
                worksheet.Cell(1, 1).Value = "Data Entrada";
                worksheet.Cell(1, 2).Value = "Hora Entrada";
                worksheet.Cell(1, 3).Value = "Tipo";
                worksheet.Cell(1, 4).Value = "Data Saída";
                worksheet.Cell(1, 5).Value = "Hora Saída";
                worksheet.Cell(1, 6).Value = "Tempo de Operação (min)";
                worksheet.Cell(1, 7).Value = "Preço Entrada";
                worksheet.Cell(1, 8).Value = "Preço Saída";
                worksheet.Cell(1, 9).Value = "Lucro";
                worksheet.Cell(1, 10).Value = "Pontos";
                worksheet.Cell(1, 11).Value = "Contratos";
                worksheet.Cell(1, 12).Value = "Total";

                for (int i = 0; i < resultados.Count; i++)
                {
                    var resultado = resultados[i];
                    worksheet.Cell(i + 2, 1).Value = resultado.DataEntrada;
                    worksheet.Cell(i + 2, 2).Value = resultado.HoraEntrada;
                    worksheet.Cell(i + 2, 3).Value = resultado.Tipo;
                    worksheet.Cell(i + 2, 4).Value = resultado.DataSaida;
                    worksheet.Cell(i + 2, 5).Value = resultado.HoraSaida;
                    worksheet.Cell(i + 2, 6).Value = resultado.TempoOperacaoMin;
                    worksheet.Cell(i + 2, 7).Value = resultado.PrecoEntrada;
                    worksheet.Cell(i + 2, 8).Value = resultado.PrecoSaida;
                    worksheet.Cell(i + 2, 9).Value = resultado.Lucro;
                    worksheet.Cell(i + 2, 10).Value = resultado.Pontos;
                    worksheet.Cell(i + 2, 11).Value = resultado.Contratos;
                    worksheet.Cell(i + 2, 12).Value = resultado.Total;
                }

                workbook.SaveAs(path);
            }
        }

        static List<Candle> LerHistoricoProfit(string path)
        {
            var linhas = File.ReadAllLines(path);
            var candles = new List<Candle>();

            foreach (var linha in linhas.Skip(1)) // Ignorar cabeçalho
            {
                var dados = linha.Split(';');

                // Combinar Data (dados[1]) e Hora (dados[2]) no formato "dd/MM/yyyy HH:mm:ss"
                DateTime dataHora = DateTime.ParseExact($"{dados[1]} {dados[2]}", "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

                // Tratar os preços: remover pontos de milhar e pegar apenas a parte inteira antes da vírgula
                int precoAbertura = int.Parse(dados[3].Replace(".", "").Split(',')[0]);
                int precoMaximo = int.Parse(dados[4].Replace(".", "").Split(',')[0]);
                int precoMinimo = int.Parse(dados[5].Replace(".", "").Split(',')[0]);
                int precoFechamento = int.Parse(dados[6].Replace(".", "").Split(',')[0]);

                candles.Add(new Candle
                {
                    DataHora = dataHora,
                    PrecoAbertura = precoAbertura,
                    PrecoMaximo = precoMaximo,
                    PrecoMinimo = precoMinimo,
                    PrecoFechamento = precoFechamento
                });
            }
            return candles;
        }

        static List<Trade> LerEntradas(string path)
        {
            var linhas = File.ReadAllLines(path);
            var trades = new List<Trade>();

            foreach (var linha in linhas)
            {
                var dados = linha.Split(';');
                string precoEntradaFormatado = dados[7].Replace(".", "").Split(',')[0]; // Removendo casas decimais e pontos

                trades.Add(new Trade
                {
                    DataHora = DateTime.ParseExact(dados[1], "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                    Tipo = dados[6],
                    PrecoEntrada = int.Parse(precoEntradaFormatado),
                    Quantidade = int.Parse(dados[5])
                });
            }
            return trades;
        }


        static List<Candle> LerHistorico(string path)
        {
            var linhas = File.ReadAllLines(path);
            var candles = new List<Candle>();

            foreach (var linha in linhas.Skip(1)) // Ignorar cabeçalho
            {
                var dados = linha.Split(',');
                candles.Add(new Candle
                {
                    DataHora = DateTime.ParseExact(dados[0], "yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture),
                    PrecoAbertura = int.Parse(dados[1].Split('.')[0]),
                    PrecoMaximo = int.Parse(dados[2].Split('.')[0]),
                    PrecoMinimo = int.Parse(dados[3].Split('.')[0]),
                    PrecoFechamento = int.Parse(dados[4].Split('.')[0])
                });
            }
            return candles;
        }

        static List<ResultadoTrade> TestarEstrategia(Estrategia estrategia, List<Trade> trades, List<Candle> historico)
        {
            var resultados = new List<ResultadoTrade>();

            foreach (var trade in trades)
            {
                Console.WriteLine($"Operação {trade.DataHora.ToString()}\n");

                DateTime tradeTime = trade.DataHora;
                DateTime tradeTimeWithoutSeconds = new DateTime(tradeTime.Year, tradeTime.Month, tradeTime.Day, tradeTime.Hour, tradeTime.Minute, 0);

                var candlesTrade = historico.Where(c => c.DataHora >= tradeTimeWithoutSeconds).OrderBy(c => c.DataHora).ToList();

                if (candlesTrade.Count == 0)
                {
                    Console.WriteLine($"[ERRO] Nenhum dado encontrado para {trade.DataHora}");
                    continue;
                }

                var primeiroCandle = candlesTrade.First();
                if (primeiroCandle != null)
                {
                    if (trade.PrecoEntrada < primeiroCandle.PrecoMinimo || trade.PrecoEntrada > primeiroCandle.PrecoMaximo)
                    {
                        Console.WriteLine($"[AVISO] Preço de entrada {trade.PrecoEntrada} está fora do range ({primeiroCandle.PrecoMinimo} - {primeiroCandle.PrecoMaximo}). Ajustando...");
                        decimal precoMedioCandle = (primeiroCandle.PrecoMaximo + primeiroCandle.PrecoMinimo) / 2;
                        trade.PrecoEntrada = Convert.ToInt32(precoMedioCandle);
                        Console.WriteLine($"[INFO] Novo preço de entrada ajustado para: {trade.PrecoEntrada}");
                    }
                }
                else
                {
                    Console.WriteLine($"[ERRO] Candle não encontrado para {tradeTimeWithoutSeconds}. Pulando trade.");
                    continue;
                }

                decimal precoSaida = trade.PrecoEntrada;
                decimal lucroFinanceiro = 0;
                int contratosParcial1 = trade.Quantidade / 2;
                int contratosParcial2 = trade.Quantidade - contratosParcial1;
                const decimal porcentagemParcial = 0.5m;
                bool p1FoiExecutado = false;
                bool stopFoiExecutado = false;
                decimal lucroPontos = 0;
                Candle candleSaida = null; // Armazenar o candle de saída

                foreach (var candle in candlesTrade)
                {
                    bool candlePositivo = candle.PrecoFechamento > candle.PrecoAbertura;
                    bool candleNegativo = candle.PrecoFechamento < candle.PrecoAbertura;
                    bool avaliarStopLossPrimeiro = candleNegativo && trade.Tipo == "C" || candlePositivo && trade.Tipo == "V" || (!candlePositivo && !candleNegativo);
                    bool avaliarParcial1Primeiro = !avaliarStopLossPrimeiro;

                    bool atingiuStopLoss = (trade.Tipo == "C" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.StopLoss) ||
                                          (trade.Tipo == "V" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.StopLoss);
                    bool atingiuP1 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial1) ||
                                    (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial1);
                    bool atingiuP2 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial2) ||
                                    (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial2);

                    if (atingiuStopLoss)
                    {
                        if (atingiuP1 && !p1FoiExecutado) // Ambos no mesmo candle
                        {
                            if (avaliarStopLossPrimeiro)
                            {
                                lucroPontos = trade.Tipo == "C" ? -estrategia.StopLoss : -estrategia.StopLoss;
                                precoSaida = trade.PrecoEntrada + lucroPontos;
                                lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                                stopFoiExecutado = true;
                                candleSaida = candle; // Armazena o candle de saída
                                Console.WriteLine($"[STOP LOSS] Priorizado no mesmo candle que P1: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                            }
                            else
                            {
                                p1FoiExecutado = true;
                                lucroPontos = estrategia.Parcial1 * porcentagemParcial;
                                precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                                lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                                candleSaida = candle; // Armazena o candle de saída
                                Console.WriteLine($"[P1 GAIN] Priorizado no mesmo candle que SL: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                            }
                        }
                        else if (p1FoiExecutado) // Stop após P1
                        {
                            lucroPontos += (trade.Tipo == "C" ? -estrategia.StopLoss : -estrategia.StopLoss) * porcentagemParcial;
                            precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? -estrategia.StopLoss : estrategia.StopLoss);
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            stopFoiExecutado = true;
                            candleSaida = candle; // Armazena o candle de saída
                            Console.WriteLine($"[STOP LOSS] Após P1: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        }
                        else // Stop sozinho
                        {
                            lucroPontos = trade.Tipo == "C" ? -estrategia.StopLoss : -estrategia.StopLoss;
                            precoSaida = trade.PrecoEntrada + lucroPontos;
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            stopFoiExecutado = true;
                            candleSaida = candle; // Armazena o candle de saída
                            Console.WriteLine($"[STOP LOSS] Sozinho: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        }

                        if (stopFoiExecutado) break;
                    }
                    else if (atingiuP1 && !p1FoiExecutado)
                    {
                        p1FoiExecutado = true;
                        lucroPontos = estrategia.Parcial1 * porcentagemParcial;
                        precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        candleSaida = candle; // Armazena o candle de saída
                        Console.WriteLine($"[P1 GAIN] Sozinho: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                    }
                    else if (atingiuP2 && p1FoiExecutado)
                    {
                        lucroPontos = estrategia.Parcial1 * porcentagemParcial + estrategia.Parcial2 * porcentagemParcial;
                        precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        candleSaida = candle; // Armazena o candle de saída
                        Console.WriteLine($"[P2 GAIN] Após P1: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        break;
                    }

                    if (!stopFoiExecutado && !p1FoiExecutado && candle == candlesTrade.Last())
                    {
                        precoSaida = candle.PrecoFechamento;
                        lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        candleSaida = candle; // Armazena o candle de saída
                        Console.WriteLine($"[SAÍDA PADRÃO] Nenhum alvo atingido, saída no fechamento: {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                    }
                }

                Console.WriteLine($"Resultado total da operação: {lucroFinanceiro}\n\n");

                // Usar o candleSaida para os dados de saída, com fallback para o último candle se não houver saída definida
                var candleFinal = candleSaida ?? candlesTrade.Last();
                var resultadoTrade = new ResultadoTrade
                {
                    DataEntrada = trade.DataHora.ToString("dd/MM/yyyy"),
                    HoraEntrada = trade.DataHora.ToString("HH:mm:ss"),
                    Tipo = trade.Tipo,
                    DataSaida = candleFinal.DataHora.ToString("dd/MM/yyyy"),
                    HoraSaida = candleFinal.DataHora.ToString("HH:mm:ss"),
                    TempoOperacaoMin = (int)(candleFinal.DataHora - trade.DataHora).TotalMinutes,
                    PrecoEntrada = trade.PrecoEntrada,
                    PrecoSaida = precoSaida,
                    Lucro = lucroFinanceiro,
                    Pontos = (int)Math.Abs(precoSaida - trade.PrecoEntrada),
                    Contratos = trade.Quantidade,
                    Total = 0 // Ajustar se necessário
                };

                resultados.Add(resultadoTrade); // Adicionar o resultado à lista
            }

            return resultados;
        }

    }

    class Trade
    {
        public DateTime DataHora { get; set; }
        public string Tipo { get; set; }
        public int PrecoEntrada { get; set; }
        public int Quantidade { get; set; }
    }

    class Candle
    {
        public DateTime DataHora { get; set; }
        public int PrecoAbertura { get; set; }
        public int PrecoMaximo { get; set; }
        public int PrecoMinimo { get; set; }
        public int PrecoFechamento { get; set; }
    }

    class Estrategia
    {
        public int StopLoss { get; set; }
        public int Parcial1 { get; set; }
        public int Parcial2 { get; set; }
    }

    class ResultadoTrade
    {
        public string DataEntrada { get; set; }
        public string HoraEntrada { get; set; }
        public string Tipo { get; set; }
        public string DataSaida { get; set; }
        public string HoraSaida { get; set; }
        public int TempoOperacaoMin { get; set; }
        public decimal PrecoEntrada { get; set; }
        public decimal PrecoSaida { get; set; }
        public decimal Lucro { get; set; }
        public int Pontos { get; set; }
        public int Contratos { get; set; }
        public decimal Total { get; set; }
    }


}
