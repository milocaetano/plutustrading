using System;

namespace SimularTrades
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;


    class Program
    {
        static void Main()
        {
            try
            {   //string entradasPath = "C:\\Relatorios\\entradas.csv"; // Arquivo com suas operações
                string entradasPath = @"C:\temp\entradas.csv"; // Arquivo com suas operações
                string historicoPath = @"C:\temp\WINFUT_F_0_1min.csv"; // Arquivo com histórico de 1 min
                string resultadoPath = @"C:\temp\resultado.csv"; // Arquivo de saída

                var entradas = LerEntradas(entradasPath);
                var historico = LerHistoricoProfit(historicoPath);
                var estrategias = GerarEstrategias(1000);

                List<string> resultados = new List<string>();

                foreach (var estrategia in estrategias)
                {
                    Console.WriteLine($"\nTestando Estratégia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
                    var resultado = TestarEstrategia(estrategia, entradas, historico);
                    resultados.Add(resultado);
                }

                File.WriteAllLines(resultadoPath, resultados);
                Console.WriteLine("Backtest concluído. Resultados salvos em resultado.csv");
            }catch(Exception ex)
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

        static List<Estrategia> GerarEstrategias(int numberEstrategias = 100)
        {
            var estrategias = new List<Estrategia>();
            var rand = new Random();

            for (int i = 0; i < numberEstrategias; i++)
            {
                int parcial1 = rand.Next(50, 300) / 5 * 5; // Multiplo de 5
                int parcial2 = rand.Next(parcial1 + 5, 701) / 5 * 5; // Garantir que P2 > P1

                //int parcial1 = rand.Next(50, 251) / 5 * 5; // Multiplo de 5
                //int parcial2 = rand.Next(parcial1 + 5, 601) / 5 * 5; // Garantir que P2 > P1


                estrategias.Add(new Estrategia
                {
                    StopLoss = rand.Next(100, 500) / 5 * 5, // Multiplo de 5
                    Parcial1 = parcial1,
                    Parcial2 = parcial2
                });
            }
            return estrategias;
        }
        //trailerAtividado nao tinha grok
        static string TestarEstrategia(Estrategia estrategia, List<Trade> trades, List<Candle> historico, bool trailerAtividado = false)
        {
            int acertos = 0, gainsParciais = 0, gainsTotais = 0, totalTrades = trades.Count, perdas = 0;
            decimal lucroTotal = 0;

            foreach (var trade in trades)
            {
                Console.WriteLine($"Operação {trade.DataHora.ToString()}\n");

                DateTime tradeTime = trade.DataHora;
                DateTime tradeTimeWithoutSeconds = new DateTime(tradeTime.Year, tradeTime.Month, tradeTime.Day, tradeTime.Hour, tradeTime.Minute, 0);
                //grok corrigir e colocar apenas a data do dia do trade para nao fazer swing trade apenas day trade
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
                foreach (var candle in candlesTrade)
                {

                    bool candlePositivo = candle.PrecoFechamento > candle.PrecoAbertura;
                    bool candleNegativo = candle.PrecoFechamento < candle.PrecoAbertura;
                    bool avaliarStopLossPrimeiro = (candleNegativo && trade.Tipo == "C") || (candlePositivo && trade.Tipo == "V") || (!candlePositivo && !candleNegativo);
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
                                lucroPontos = trade.Tipo == "C" ? -estrategia.StopLoss : estrategia.StopLoss;
                                precoSaida = trade.PrecoEntrada + lucroPontos;
                                lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                                stopFoiExecutado = true;
                                perdas++;
                                Console.WriteLine($"[STOP LOSS] Priorizado no mesmo candle que P1: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                            }
                            else
                            {
                                p1FoiExecutado = true;
                                lucroPontos = estrategia.Parcial1 * porcentagemParcial;
                                precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                                lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                                gainsParciais++;
                                Console.WriteLine($"[P1 GAIN] Priorizado no mesmo candle que SL: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                            }
                        }
                        else if (p1FoiExecutado) // Stop após P1
                        {
                            lucroPontos += (trade.Tipo == "C" ? -estrategia.StopLoss : estrategia.StopLoss) * porcentagemParcial;
                            precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? -estrategia.StopLoss : estrategia.StopLoss);
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            stopFoiExecutado = true;
                            perdas++;
                            Console.WriteLine($"[STOP LOSS] Após P1: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        }
                        else // Stop sozinho
                        {
                            lucroPontos = trade.Tipo == "C" ? -estrategia.StopLoss : estrategia.StopLoss;
                            precoSaida = trade.PrecoEntrada + lucroPontos;
                            lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                            stopFoiExecutado = true;
                            perdas++;
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
                        gainsParciais++;
                        Console.WriteLine($"[P1 GAIN] Sozinho: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                    }
                    else if (atingiuP2 && p1FoiExecutado)
                    {
                        lucroPontos = estrategia.Parcial1 * porcentagemParcial + estrategia.Parcial2 * porcentagemParcial;
                        precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        gainsTotais++;
                        Console.WriteLine($"[P2 GAIN] Após P1: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        break;
                    }


                  //  //corrige isso daqui. Tipo se tiver feito parcial p1. pega a diferença entre o fechamento do dia com preço de entrada. Calcula  (porcentagemParcial * (diferença) +  lestrategia.Parcial1 * porcentagemParcial)
                  //assim vc calcula o lucroPontos e depois o lucro financeiro com base nos codigos anterior se inspira ai 
                  // e se nao bateu p1. Vc vai calc
                    
                    if (!stopFoiExecutado && !p1FoiExecutado && candle == candlesTrade.Last())
                    {
                        precoSaida = candle.PrecoFechamento;
                        lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        Console.WriteLine($"[SAÍDA PADRÃO] Nenhum alvo atingido, saída no fechamento: {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                    }
                }

                Console.WriteLine($"Resutlado total da operação:  {lucroFinanceiro}\n\n");

                if (lucroFinanceiro > 0) acertos++;
                lucroTotal += lucroFinanceiro;
            }

            decimal taxaAcerto = totalTrades > 0 ? (decimal)acertos / totalTrades * 100 : 0;
            decimal taxaGainParcial = totalTrades > 0 ? (decimal)gainsParciais / totalTrades * 100 : 0;
            decimal taxaGainTotal = totalTrades > 0 ? (decimal)gainsTotais / totalTrades * 100 : 0;
            decimal taxaPrejuizo = totalTrades > 0 ? (decimal)perdas / totalTrades * 100 : 0;
            decimal lucroMedio = totalTrades > 0 ? lucroTotal / totalTrades : 0;

            Console.WriteLine("\n===== RESULTADO DA ESTRATÉGIA =====");
            Console.WriteLine($"Estrategia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
            Console.WriteLine("-----------------------------------");
            Console.WriteLine($"Total de Trades: {totalTrades}");
            Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}%");
            Console.WriteLine($"Taxa de Prejuízo: {taxaPrejuizo:F2}%");
            Console.WriteLine($"Taxa de Gain Parcial: {taxaGainParcial:F2}%");
            Console.WriteLine($"Taxa de Gain Total: {taxaGainTotal:F2}%");
            Console.WriteLine($"Lucro Médio por Trade: R${lucroMedio:F2}");
            Console.WriteLine($"Lucro Total: R${lucroTotal:F2}");
            Console.WriteLine("===================================\n");

            return $"Estrategia {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2};{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainTotal:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
        }
        static string TestarEstrategia_camilo(Estrategia estrategia, List<Trade> trades, List<Candle> historico)
        {
            int acertos = 0, gainsParciais = 0, gainsTotais = 0, totalTrades = trades.Count, perdas = 0;
            decimal lucroTotal = 0;

            foreach (var trade in trades)
            {
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

                        // Calcular o preço médio do candle (média entre máxima e mínima)
                        decimal precoMedioCandle = (primeiroCandle.PrecoMaximo + primeiroCandle.PrecoMinimo) / 2;
                        // Ajustar o preço de entrada para esse preço médio
                        Console.WriteLine($"[AJUSTE] Trade original: {trade.PrecoEntrada}, Novo Preço de Entrada: {precoMedioCandle}");
                        trade.PrecoEntrada = Convert.ToInt32(precoMedioCandle);

                        Console.WriteLine($"[INFO] Novo preço de entrada ajustado para: {trade.PrecoEntrada}");
                    }
                }
                else
                {
                    Console.WriteLine($"[ERRO] Candle não encontrado para {tradeTimeWithoutSeconds}. Pulando trade.");
                    continue;
                }

                // **Definição da ordem de avaliação**
                bool avaliarStopLossPrimeiro = false, avaliarParcial1Primeiro = false, parcial2 = false;
                decimal precoSaida = trade.PrecoEntrada;
                decimal lucroFinanceiro = 0;
                int contratosParcial1 = trade.Quantidade / 2;
                int contratosParcial2 = trade.Quantidade - contratosParcial1;
                decimal lucroPontos = 0;
                const decimal porcentagemParcial = 0.5m;
               
                bool p1FoiExecutado = false;
                bool stopFoiExecutado = false;
            

                // **Percorre os candles minuto a minuto**
                foreach (var candle in candlesTrade)
                {
                    bool candlePositivo = candle.PrecoFechamento > candle.PrecoAbertura;
                    bool candleNegativo = candle.PrecoFechamento < candle.PrecoAbertura;
                    if (candlePositivo) 
                    {
                        if (trade.Tipo == "C") avaliarParcial1Primeiro = true; 
                        else avaliarStopLossPrimeiro = true; 
                    }
                    else if (candleNegativo)
                    {
                        if (trade.Tipo == "C") avaliarStopLossPrimeiro = true; 
                        else avaliarParcial1Primeiro = true;
                    }else
                    {
                        avaliarStopLossPrimeiro = true;
                    }

                    bool atingiuStopLoss = (trade.Tipo == "C" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.StopLoss) ||
                        (trade.Tipo == "V" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.StopLoss);

                    bool atingiuP1 =  (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial1) ||
                                          (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial1);


                    bool atingiuP2 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial2) ||
                                          (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial2);

                    if(atingiuStopLoss)
                    {                        
                        if (atingiuP1)
                        {
                            if (!p1FoiExecutado) //atingiu p1, stop loss junto e p1 ainda nao havia sido executado em candle anterior
                            {
                                if (avaliarStopLossPrimeiro) //se avaliar primeiro o loss entao vamos calcular e sair fora
                                {
                                    lucroPontos = (trade.Tipo == "C"
                                   ? -(estrategia.StopLoss)
                                   : estrategia.StopLoss); 

                                    precoSaida = trade.PrecoEntrada + lucroPontos;

                                    lucroFinanceiro = (lucroPontos * trade.Quantidade * 0.2m);//prejuizo cheio
                                    stopFoiExecutado = true;
                                    perdas++;

                                    Console.WriteLine($"[STOP LOSS] Atingiu no mesmo Trade encerrado: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");

                                }
                                else //p1 foi executado primeiro no mesmo candle e sera avialiado primeiro, logo o stop nao sera executado
                                {
                                    p1FoiExecutado =true;
                                    stopFoiExecutado = false;
                                    lucroPontos = (trade.Tipo == "C"
                                   ? estrategia.Parcial1
                                   : -estrategia.Parcial1) * porcentagemParcial; //lucro parcial1                                  
                                    precoSaida = trade.PrecoEntrada + lucroPontos;

                                    lucroFinanceiro = (lucroPontos * trade.Quantidade * 0.2m);
                                    gainsParciais++;

                                    Console.WriteLine($"[P1 GAIN] Atingiu no mesmo Trade que loss: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                                }
                            }
                            else if (p1FoiExecutado)
                            {
                                lucroPontos = lucroPontos + (trade.Tipo == "C"
                                    ? -(estrategia.StopLoss)
                                    : estrategia.StopLoss) * porcentagemParcial; //lucro atual terá parte do lucro parcial p1 + mais prejuizo parcial do loss

                                precoSaida = trade.PrecoEntrada + lucroPontos;

                                lucroFinanceiro = (lucroPontos * trade.Quantidade * 0.2m);//prejuizo cheio
                                stopFoiExecutado = true;
                                perdas++;

                                Console.WriteLine($"[STOP LOSS] Atingiu no mesmo Trade após p1 ter sido executado em algum outro candle anterior: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                            }
                        }                        
                        else //stop loss atingiu sozinho
                        {
                            lucroPontos = (trade.Tipo == "C"
                                    ? -(estrategia.StopLoss)
                                    : estrategia.StopLoss);

                            precoSaida = trade.PrecoEntrada + lucroPontos;

                            lucroFinanceiro = (lucroPontos * trade.Quantidade * 0.2m);//prejuizo cheio
                            stopFoiExecutado = true;
                            perdas++;

                            Console.WriteLine($"[STOP LOSS] Atingiu primeiro que todo mundo: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        }

                        if (stopFoiExecutado)
                        {
                            break;
                        }
                    }
                    else if(atingiuP1)
                    {
                        p1FoiExecutado = true;                      
                        lucroPontos = (trade.Tipo == "C"
                       ? estrategia.Parcial1
                       : -estrategia.Parcial1) * porcentagemParcial; //lucro parcial1                                  
                        precoSaida = trade.PrecoEntrada + lucroPontos;

                        lucroFinanceiro = (lucroPontos * trade.Quantidade * 0.2m);
                        gainsParciais++;

                        Console.WriteLine($"[P1 GAIN] Atingiu no mesmo Trade que loss: Entrada {trade.PrecoEntrada}, Saída {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                    }
                    else if(atingiuP2){

                        if (!p1FoiExecutado)
                        {
                            throw new Exception("Erro p2 nao pode atingir antes do p1");
                        }
                        lucroPontos = lucroPontos + ((trade.Tipo == "C"
                         ? estrategia.Parcial2
                         : -estrategia.Parcial2) * porcentagemParcial); //soma lucro em pontos de p1 + p2

                        lucroFinanceiro = (lucroPontos * trade.Quantidade * 0.2m);

                        Console.WriteLine($"[P2] Trade parcial executado: Entrada {trade.PrecoEntrada}, Parcial 1 atingida em {precoSaida}, Lucro P2: {lucroFinanceiro:F2}");
                        gainsTotais++;
                        break;
                    }           
                }

                if (lucroFinanceiro > 0) acertos++;
                lucroTotal += lucroFinanceiro;
            }

            decimal taxaAcerto = totalTrades > 0 ? (decimal)acertos / totalTrades * 100 : 0;
            decimal taxaGainParcial = totalTrades > 0 ? (decimal)gainsParciais / totalTrades * 100 : 0;
            decimal taxaGainTotal = totalTrades > 0 ? (decimal)gainsTotais / totalTrades * 100 : 0;
            decimal taxaPrejuizo = totalTrades > 0 ? (decimal)perdas / totalTrades * 100 : 0;
            decimal lucroMedio = totalTrades > 0 ? lucroTotal / totalTrades : 0;

            Console.WriteLine("\n===== RESULTADO DA ESTRATÉGIA =====");
            Console.WriteLine($"Estrategia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
            Console.WriteLine("-----------------------------------");
            Console.WriteLine($"Total de Trades: {totalTrades}");
            Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}%");
            Console.WriteLine($"Taxa de Prejuízo: {taxaPrejuizo:F2}%");
            Console.WriteLine($"Taxa de Gain Parcial: {taxaGainParcial:F2}%");
            Console.WriteLine($"Taxa de Gain Total: {taxaGainTotal:F2}%");
            Console.WriteLine($"Lucro Médio por Trade: R${lucroMedio:F2}");
            Console.WriteLine($"Lucro Total: R${lucroTotal:F2}");
            Console.WriteLine("===================================\n");

            return $"Estrategia {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2};{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainTotal:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
        }



        //static string TestarEstrategia_OLD(Estrategia estrategia, List<Trade> trades, List<Candle> historico)
        //{
        //    int acertos = 0, gainsParciais = 0, gainsTotais = 0, totalTrades = trades.Count;
        //    decimal lucroTotal = 0;

        //    foreach (var trade in trades)
        //    {
        //        DateTime tradeTime = trade.DataHora;
        //        DateTime tradeTimeWithoutSeconds = new DateTime(tradeTime.Year, tradeTime.Month, tradeTime.Day, tradeTime.Hour, tradeTime.Minute, 0);

        //        var candle = historico.FirstOrDefault(c => c.DataHora == tradeTimeWithoutSeconds);
        //        if (candle == null)
        //        {
        //            Console.WriteLine($"[AVISO] Trade em {trade.DataHora} não encontrado no histórico. Tentando aproximação...");
        //            var candleAproximado = historico.FirstOrDefault(c => Math.Abs((c.DataHora - tradeTimeWithoutSeconds).TotalMinutes) <= 1);
        //            if (candleAproximado != null)
        //            {
        //                candle = candleAproximado;
        //                Console.WriteLine($"[INFO] Usando candle de {candle.DataHora} para o trade {trade.DataHora}");
        //            }
        //            else
        //            {
        //                Console.WriteLine($"[ERRO] Nenhum dado encontrado para {trade.DataHora}, nem mesmo aproximado.");
        //                continue;
        //            }
        //        }

        //        bool stopLoss = (trade.Tipo == "C" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.StopLoss) ||
        //                    (trade.Tipo == "V" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.StopLoss);

        //        bool parcial1 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial1) ||
        //                        (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial1);

        //        bool parcial2 = parcial1 && ((trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial2) ||
        //                                     (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial2));

        //        decimal precoSaida = trade.PrecoEntrada;
        //        decimal lucro = 0;
        //        int contratosParcial1 = trade.Quantidade / 2;
        //        int contratosParcial2 = trade.Quantidade - contratosParcial1;

        //        if (parcial1 && stopLoss)
        //        {
        //            decimal precoParcial1 = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial1 : -estrategia.Parcial1);
        //            decimal precoStop = trade.PrecoEntrada - (trade.Tipo == "C" ? estrategia.StopLoss : -estrategia.StopLoss);
        //            precoSaida = ((precoParcial1 * contratosParcial1) + (precoStop * contratosParcial2)) / (contratosParcial1 + contratosParcial2);
        //            lucro = ((precoSaida - trade.PrecoEntrada) * trade.Quantidade) * 0.2m;
                   
        //            if(lucro > 0)
        //            {
        //                gainsParciais++;
        //            }
        //        }
        //        else if (stopLoss)
        //        {
        //            precoSaida = trade.Tipo == "C"
        //                ? trade.PrecoEntrada - estrategia.StopLoss
        //                : trade.PrecoEntrada + estrategia.StopLoss;

        //            lucro = (trade.Tipo == "C"
        //                ? (precoSaida - trade.PrecoEntrada)
        //                : (trade.PrecoEntrada - precoSaida)) * trade.Quantidade * 0.2m;
        //        }


        //        else if (parcial2)
        //        {
        //            decimal precoParcial1 = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial1 : -estrategia.Parcial1);
        //            decimal precoParcial2 = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial2 : -estrategia.Parcial2);
        //            precoSaida = ((precoParcial1 * contratosParcial1) + (precoParcial2 * contratosParcial2)) / trade.Quantidade;
        //            lucro = ((precoSaida - trade.PrecoEntrada) * trade.Quantidade) * 0.2m;
        //            gainsTotais++;
        //        }
        //        else if (parcial1)
        //        {
        //            precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial1 : -estrategia.Parcial1);
        //            lucro = ((precoSaida - trade.PrecoEntrada) * trade.Quantidade) * 0.2m;
        //            gainsParciais++;
        //        }
        //        else
        //        {
        //            var ultimoCandle = historico.LastOrDefault(c => c.DataHora.Date == trade.DataHora.Date);
        //            if (ultimoCandle != null)
        //            {
        //                precoSaida = ultimoCandle.PrecoFechamento;
        //                lucro = ((precoSaida - trade.PrecoEntrada) * trade.Quantidade) * 0.2m;
        //            }
        //        }                   

        //        if (lucro > 0) acertos++;

        //        lucroTotal += lucro;

        //        Console.WriteLine($"Trade: {trade.DataHora} | Entrada: {trade.PrecoEntrada} | Saída: {precoSaida} | Lucro: R${lucro:F2}");
        //        Console.WriteLine($"Resultado Total: R${lucroTotal:F2}");
        //    }

        //    decimal taxaAcerto = totalTrades > 0 ? (decimal)acertos / totalTrades * 100 : 0;
        //    decimal taxaGainParcial = totalTrades > 0 ? (decimal)gainsParciais / totalTrades * 100 : 0;
        //    decimal taxaGainTotal = totalTrades > 0 ? (decimal)gainsTotais / totalTrades * 100 : 0;
        //    decimal lucroMedio = totalTrades > 0 ? lucroTotal / totalTrades : 0;
        //    decimal taxaPrejuizo = 100 - taxaAcerto;

        //    Console.WriteLine("\n===== RESULTADO DA ESTRATÉGIA =====");
        //    Console.WriteLine($"Estrategia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
        //    Console.WriteLine("-----------------------------------");
        //    Console.WriteLine($"Total de Trades: {totalTrades}");
        //    Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}%");
        //    Console.WriteLine($"Taxa de Prejuízo: {taxaPrejuizo:F2}%");
        //    Console.WriteLine($"Taxa de Gain Parcial: {taxaGainParcial:F2}%");
        //    Console.WriteLine($"Taxa de Gain Total: {taxaGainTotal:F2}%");
        //    Console.WriteLine($"Lucro Médio por Trade: R${lucroMedio:F2}");
        //    Console.WriteLine($"Lucro Total: R${lucroTotal:F2}");
        //    Console.WriteLine("===================================\n");


        //    return $"Estrategia {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2};{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainTotal:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
        //}

    }

    class Trade
    {
        public DateTime DataHora { get; set; }
        public string Tipo { get; set; } // C ou V
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
}
