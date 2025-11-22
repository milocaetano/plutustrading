using System;

namespace SimularTradesAdvancedTrailing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    namespace SimularTradesAdvancedTrailing
    {
        class Program
        {
            static void Main()
            {
                try
                {
                    string entradasPath = "C:\\temp\\entradas.csv";
                    string historicoPath = "C:\\temp\\WINFUT_F_0_1min.csv";
                    string resultadoPath = "C:\\temp\\resultado_advanced.csv";

                    var entradas = LerEntradas(entradasPath);
                    var historico = LerHistoricoProfit(historicoPath);

                    var estrategias = GerarEstrategias();

                    List<string> resultados = new List<string>();

                    foreach (var estrategia in estrategias)
                    {
                        Console.WriteLine($"\n[INÍCIO] Testando Estratégia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}{(estrategia.Parcial3.HasValue ? $", P3={estrategia.Parcial3}" : "")}, Trailer={(estrategia.TrailerTriggerPercentage.HasValue ? $"{estrategia.TrailerTriggerPercentage}%" : "Nenhum")}");
                        var resultado = TestarEstrategia(estrategia, entradas, historico);

                        // Escrever no arquivo imediatamente após o teste
                        using (var writer = new StreamWriter(resultadoPath, true))
                        {
                            writer.WriteLine(resultado);
                        }
                        Console.WriteLine($"[FIM] Resultado gravado: {resultado}");
                    }

                    Console.WriteLine("Backtest concluído. Resultados salvos em resultado_advanced.csv");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
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

            static List<Candle> LerHistoricoProfit(string path)
            {
                var linhas = File.ReadAllLines(path);
                var candles = new List<Candle>();

                foreach (var linha in linhas.Skip(1)) // Ignorar cabeçalho
                {
                    var dados = linha.Split(';');
                    DateTime dataHora = DateTime.ParseExact($"{dados[1]} {dados[2]}", "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
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

            static List<Estrategia> GerarEstrategias()
            {
                var estrategias = new List<Estrategia>();
                var rand = new Random();

                // Intervalos para reduzir combinações
                int[] stopLossRange = Enumerable.Range(150, 500).Where(x => x % 15 == 0).ToArray(); // 100 a 450, passo 50
                int[] parcial1Range = Enumerable.Range(60, 251).Where(x => x % 20 == 0).ToArray(); // 50 a 300, passo 50

                foreach (int sl in stopLossRange)
                {
                    foreach (int p1 in parcial1Range)
                    {
                        for (int p2 = p1 + 50; p2 <= 600; p2 += 50)
                        {
                            // Sem P3, sem trailer
                            estrategias.Add(new Estrategia { StopLoss = sl, Parcial1 = p1, Parcial2 = p2 });

                            // Com P3, sem trailer
                            for (int p3 = p2 + 50; p3 <= 900; p3 += 50)
                            {
                                estrategias.Add(new Estrategia { StopLoss = sl, Parcial1 = p1, Parcial2 = p2, Parcial3 = p3 });

                                // Com P3, com trailer
                                for (int trailer = 10; trailer <= 50; trailer += 5)
                                {
                                    estrategias.Add(new Estrategia { StopLoss = sl, Parcial1 = p1, Parcial2 = p2, Parcial3 = p3, TrailerTriggerPercentage = trailer });
                                }
                            }

                            // Sem P3, com trailer
                            for (int trailer = 10; trailer <= 50; trailer += 5)
                            {
                                estrategias.Add(new Estrategia { StopLoss = sl, Parcial1 = p1, Parcial2 = p2, TrailerTriggerPercentage = trailer });
                            }
                        }
                    }
                }

                return estrategias;
            }

            static string TestarEstrategia(Estrategia estrategia, List<Trade> trades, List<Candle> historico)
            {
                int acertos = 0, gainsParciais = 0, gainsParciais2 = 0, gainsTotais = 0, totalTrades = trades.Count, perdas = 0;
                decimal lucroTotal = 0;

                foreach (var trade in trades)
                {
                    DateTime tradeTime = trade.DataHora;
                    DateTime tradeTimeWithoutSeconds = new DateTime(tradeTime.Year, tradeTime.Month, tradeTime.Day, tradeTime.Hour, tradeTime.Minute, 0);
                    DateTime nextDayWithoutSeconds = tradeTime.Date.AddDays(1).AddHours(23).AddMinutes(59);

                    var candlesTrade = historico
                        .Where(c => c.DataHora >= tradeTimeWithoutSeconds && c.DataHora <= nextDayWithoutSeconds)
                        .OrderBy(c => c.DataHora)
                        .ToList();

                    if (candlesTrade.Count == 0)
                    {
                        Console.WriteLine($"[ERRO] Nenhum dado encontrado para o dia {trade.DataHora.Date}");
                        continue;
                    }

                    var primeiroCandle = candlesTrade.First();
                    if (trade.PrecoEntrada < primeiroCandle.PrecoMinimo || trade.PrecoEntrada > primeiroCandle.PrecoMaximo)
                    {
                        trade.PrecoEntrada = (primeiroCandle.PrecoMaximo + primeiroCandle.PrecoMinimo) / 2;
                    }

                    decimal precoSaida = trade.PrecoEntrada;
                    decimal lucroFinanceiro = 0;
                    bool p1Executado = false, p2Executado = false;
                    bool trailerAtivado = false;
                    int trailingStopLevel = trade.Tipo == "C" ? trade.PrecoEntrada - estrategia.StopLoss : trade.PrecoEntrada + estrategia.StopLoss;
                    decimal triggerLevel = (estrategia.Parcial3 ?? estrategia.Parcial2) * (estrategia.TrailerTriggerPercentage.GetValueOrDefault(0) / 100m);
                    decimal[] percentagens = estrategia.Parcial3.HasValue ? new[] { 0.33m, 0.33m, 0.34m } : new[] { 0.5m, 0.5m };
                    int[] contratos = estrategia.Parcial3.HasValue
                        ? new[] { trade.Quantidade / 3, trade.Quantidade / 3, trade.Quantidade - 2 * (trade.Quantidade / 3) }
                        : new[] { trade.Quantidade / 2, trade.Quantidade - trade.Quantidade / 2 };
                    int contratosRestantes = trade.Quantidade;
                    bool primeiroCandleProcessado = false;

                    foreach (var candle in candlesTrade)
                    {
                        bool atingiuStopLoss = (trade.Tipo == "C" && candle.PrecoMinimo <= trailingStopLevel) ||
                                               (trade.Tipo == "V" && candle.PrecoMaximo >= trailingStopLevel);
                        bool atingiuP1 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial1) ||
                                         (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial1);
                        bool atingiuP2 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial2) ||
                                         (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial2);
                        bool atingiuP3 = estrategia.Parcial3.HasValue && ((trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial3.Value) ||
                                                                          (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial3.Value));
                        bool atingiuTrigger = estrategia.TrailerTriggerPercentage.HasValue && ((trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + triggerLevel) ||
                                                                                               (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - triggerLevel));

                        // Lógica para o primeiro candle
                        if (!primeiroCandleProcessado)
                        {
                            primeiroCandleProcessado = true;

                            if (atingiuStopLoss && atingiuP1)
                            {
                                // Priorizar stop loss no primeiro candle
                                precoSaida = trailingStopLevel;
                                lucroFinanceiro = (trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida) * trade.Quantidade * 0.2m;
                                perdas++;
                                break;
                            }
                            else if (atingiuStopLoss)
                            {
                                precoSaida = trailingStopLevel;
                                lucroFinanceiro = (trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida) * trade.Quantidade * 0.2m;
                                perdas++;
                                break;
                            }
                            else if (atingiuP1)
                            {
                                p1Executado = true;
                                precoSaida = trade.Tipo == "C" ? trade.PrecoEntrada + estrategia.Parcial1 : trade.PrecoEntrada - estrategia.Parcial1;
                                lucroFinanceiro += estrategia.Parcial1 * contratos[0] * 0.2m;
                                contratosRestantes -= contratos[0];
                                gainsParciais++;
                            }
                        }
                        else
                        {
                            if (atingiuStopLoss)
                            {
                                precoSaida = trailingStopLevel;
                                decimal lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                                lucroFinanceiro += lucroPontos * contratosRestantes * 0.2m;
                                perdas++;
                                break;
                            }

                            if (atingiuP1 && !p1Executado)
                            {
                                p1Executado = true;
                                precoSaida = trade.Tipo == "C" ? trade.PrecoEntrada + estrategia.Parcial1 : trade.PrecoEntrada - estrategia.Parcial1;
                                lucroFinanceiro += estrategia.Parcial1 * contratos[0] * 0.2m;
                                contratosRestantes -= contratos[0];
                                gainsParciais++;
                            }

                            if (atingiuP2 && p1Executado && !p2Executado)
                            {
                                p2Executado = true;
                                precoSaida = trade.Tipo == "C" ? trade.PrecoEntrada + estrategia.Parcial2 : trade.PrecoEntrada - estrategia.Parcial2;
                                lucroFinanceiro += estrategia.Parcial2 * contratos[1] * 0.2m;
                                contratosRestantes -= contratos[1];
                                gainsParciais2++;
                            }

                            if (atingiuP3 && p2Executado)
                            {
                                precoSaida = trade.Tipo == "C" ? trade.PrecoEntrada + estrategia.Parcial3.Value : trade.PrecoEntrada - estrategia.Parcial3.Value;
                                lucroFinanceiro += estrategia.Parcial3.Value * contratos[2] * 0.2m;
                                contratosRestantes = 0;
                                gainsTotais++;
                                break;
                            }

                            if (atingiuTrigger && !trailerAtivado)
                            {
                                trailerAtivado = true;
                            }

                            if (trailerAtivado)
                            {
                                if (trade.Tipo == "C" && candle.PrecoMaximo > trailingStopLevel + estrategia.StopLoss)
                                    trailingStopLevel = candle.PrecoMaximo - estrategia.StopLoss;
                                else if (trade.Tipo == "V" && candle.PrecoMinimo < trailingStopLevel - estrategia.StopLoss)
                                    trailingStopLevel = candle.PrecoMinimo + estrategia.StopLoss;
                            }
                        }

                        if (candle == candlesTrade.Last() && !atingiuStopLoss && contratosRestantes > 0)
                        {
                            precoSaida = candle.PrecoFechamento;
                            decimal lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                            lucroFinanceiro += lucroPontos * contratosRestantes * 0.2m;
                        }
                    }

                    // Acerto só é contado se o lucro líquido for positivo
                    if (lucroFinanceiro > 0) acertos++;
                    lucroTotal += lucroFinanceiro;
                }

                decimal taxaAcerto = totalTrades > 0 ? (decimal)acertos / totalTrades * 100 : 0;
                decimal taxaGainParcial = totalTrades > 0 ? (decimal)gainsParciais / totalTrades * 100 : 0;
                decimal taxaGainParcial2 = totalTrades > 0 ? (decimal)gainsParciais2 / totalTrades * 100 : 0;
                decimal taxaGainTotal = totalTrades > 0 ? (decimal)gainsTotais / totalTrades * 100 : 0;
                decimal taxaPrejuizo = totalTrades > 0 ? (decimal)perdas / totalTrades * 100 : 0;
                decimal lucroMedio = totalTrades > 0 ? lucroTotal / totalTrades : 0;

                string trailerInfo = estrategia.TrailerTriggerPercentage.HasValue ? $"-Trailer{estrategia.TrailerTriggerPercentage}%" : "-SemTrailer";
                string p3Info = estrategia.Parcial3.HasValue ? $"-{estrategia.Parcial3}" : "";

                Console.WriteLine($"\nEstrategia Sem Trailer: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
                Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}% | Gain Parcial: {taxaGainParcial:F2}% | Gain Total: {taxaGainTotal:F2}% | Prejuízo: {taxaPrejuizo:F2}%");
                Console.WriteLine($"Lucro Médio: R${lucroMedio:F2} | Lucro Total: R${lucroTotal:F2}");

                return $"Estrategia {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2}{p3Info}{trailerInfo};{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainParcial2:F2}%;{taxaGainTotal:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
            }

            static string TestarEstrategia_old(Estrategia estrategia, List<Trade> trades, List<Candle> historico)
            {
                int acertos = 0, gainsParciais = 0, gainsParciais2 = 0, gainsTotais = 0, totalTrades = trades.Count, perdas = 0;
                decimal lucroTotal = 0;

                foreach (var trade in trades)
                {
                    DateTime tradeTime = trade.DataHora;
                    DateTime nextDay = tradeTime.AddDays(1);

                    DateTime tradeTimeWithoutSeconds = new DateTime(tradeTime.Year, tradeTime.Month, tradeTime.Day, tradeTime.Hour, tradeTime.Minute, 0);
                    DateTime nextDayWithoutSeconds = new DateTime(nextDay.Year, nextDay.Month, nextDay.Day, nextDay.Hour, nextDay.Minute, 0);

                    var candlesTrade = historico
                        .Where(c => c.DataHora >= tradeTimeWithoutSeconds && c.DataHora < nextDayWithoutSeconds)
                        .OrderBy(c => c.DataHora)
                        .ToList();

                    if (candlesTrade.Count == 0)
                    {
                        Console.WriteLine($"[ERRO] Nenhum dado encontrado para o dia {trade.DataHora.Date}");
                        continue;
                    }

                    var primeiroCandle = candlesTrade.First();
                    if (trade.PrecoEntrada < primeiroCandle.PrecoMinimo || trade.PrecoEntrada > primeiroCandle.PrecoMaximo)
                    {
                        trade.PrecoEntrada = (primeiroCandle.PrecoMaximo + primeiroCandle.PrecoMinimo) / 2;
                    }

                    decimal precoSaida = trade.PrecoEntrada;
                    decimal lucroFinanceiro = 0;
                    decimal lucroPontos = 0;
                    bool p1Executado = false, p2Executado = false;
                    bool trailerAtivado = estrategia.TrailerTriggerPercentage.HasValue;
                    int trailingStopLevel = trade.PrecoEntrada - (trade.Tipo == "C" ? estrategia.StopLoss : -estrategia.StopLoss);
                    decimal triggerLevel = (estrategia.Parcial3 ?? estrategia.Parcial2) * (estrategia.TrailerTriggerPercentage.GetValueOrDefault(0) / 100m);
                    decimal[] percentagens = estrategia.Parcial3.HasValue ? new[] { 0.33m, 0.33m, 0.34m } : new[] { 0.5m, 0.5m };
                    int[] contratos = estrategia.Parcial3.HasValue
                        ? new[] { trade.Quantidade / 3, trade.Quantidade / 3, trade.Quantidade - 2 * (trade.Quantidade / 3) }
                        : new[] { trade.Quantidade / 2, trade.Quantidade - trade.Quantidade / 2 };

                    foreach (var candle in candlesTrade)
                    {
                        bool atingiuStopLoss = (trade.Tipo == "C" && candle.PrecoMinimo <= trailingStopLevel) ||
                                               (trade.Tipo == "V" && candle.PrecoMaximo >= trailingStopLevel);
                        bool atingiuP1 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial1) ||
                                         (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial1);
                        bool atingiuP2 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial2) ||
                                         (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial2);
                        bool atingiuP3 = estrategia.Parcial3.HasValue && ((trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial3.Value) ||
                                                                          (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial3.Value));
                        bool atingiuTrigger = trailerAtivado && ((trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + triggerLevel) ||
                                                                 (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - triggerLevel));

                        if (atingiuStopLoss)
                        {
                            precoSaida = trailingStopLevel;
                            lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            if (p1Executado) lucroFinanceiro += estrategia.Parcial1 * percentagens[0] * trade.Quantidade * 0.2m;
                            if (p2Executado) lucroFinanceiro += estrategia.Parcial2 * percentagens[1] * trade.Quantidade * 0.2m;
                            perdas++;
                            break;
                        }

                        if (atingiuP1 && !p1Executado)
                        {
                            p1Executado = true;
                            lucroPontos = estrategia.Parcial1 * percentagens[0];
                            precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            gainsParciais++;
                        }

                        if (atingiuP2 && p1Executado && !p2Executado)
                        {
                            p2Executado = true;
                            lucroPontos += estrategia.Parcial2 * percentagens[1];
                            precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial2 : -estrategia.Parcial2);
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            gainsParciais2++;
                        }

                        if (atingiuP3 && p2Executado)
                        {
                            lucroPontos += estrategia.Parcial3.Value * percentagens[2];
                            precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial3.Value : -estrategia.Parcial3.Value);
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            gainsTotais++;
                            break;
                        }

                        if (atingiuTrigger && trailerAtivado && !trailerAtivado)
                        {
                            trailerAtivado = true;
                            trailingStopLevel = trade.PrecoEntrada;
                        }

                        if (trailerAtivado)
                        {
                            if (trade.Tipo == "C" && candle.PrecoMaximo > trailingStopLevel + estrategia.StopLoss)
                                trailingStopLevel = candle.PrecoMaximo - estrategia.StopLoss;
                            else if (trade.Tipo == "V" && candle.PrecoMinimo < trailingStopLevel - estrategia.StopLoss)
                                trailingStopLevel = candle.PrecoMinimo + estrategia.StopLoss;
                        }

                        if (candle == candlesTrade.Last() && !atingiuStopLoss)
                        {
                            precoSaida = candle.PrecoFechamento;
                            lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            if (p1Executado) lucroFinanceiro += estrategia.Parcial1 * percentagens[0] * trade.Quantidade * 0.2m;
                            if (p2Executado) lucroFinanceiro += estrategia.Parcial2 * percentagens[1] * trade.Quantidade * 0.2m;
                            break;
                        }
                    }

                    if (lucroFinanceiro > 0) acertos++;
                    lucroTotal += lucroFinanceiro;
                    Console.WriteLine($"[RESULTADO TRADE] Lucro Acumulado: R${lucroTotal:F2}");
                }

                decimal taxaAcerto = totalTrades > 0 ? (decimal)acertos / totalTrades * 100 : 0;
                decimal taxaGainParcial = totalTrades > 0 ? (decimal)gainsParciais / totalTrades * 100 : 0;
                decimal taxaGainParcial2 = totalTrades > 0 ? (decimal)gainsParciais2 / totalTrades * 100 : 0;
                decimal taxaGainTotal = totalTrades > 0 ? (decimal)gainsTotais / totalTrades * 100 : 0;
                decimal taxaPrejuizo = totalTrades > 0 ? (decimal)perdas / totalTrades * 100 : 0;
                decimal lucroMedio = totalTrades > 0 ? lucroTotal / totalTrades : 0;

                string trailerInfo = estrategia.TrailerTriggerPercentage.HasValue ? $"-Trailer{estrategia.TrailerTriggerPercentage}%" : "-SemTrailer";
                string p3Info = estrategia.Parcial3.HasValue ? $"-{estrategia.Parcial3}" : "";
                Console.WriteLine($"\n[RESUMO] Estratégia: {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2}{p3Info}{trailerInfo}");
                Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}% | Gain P1: {taxaGainParcial:F2}% | Gain P2: {taxaGainParcial2:F2}% | Gain Total: {taxaGainTotal:F2}% | Prejuízo: {taxaPrejuizo:F2}%");
                Console.WriteLine($"Lucro Médio: R${lucroMedio:F2} | Lucro Total: R${lucroTotal:F2}");
                return $"Estrategia {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2}{p3Info}{trailerInfo};{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainParcial2:F2}%;{taxaGainTotal:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
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
            public int? Parcial3 { get; set; }
            public int? TrailerTriggerPercentage { get; set; }
        }
    }
}
