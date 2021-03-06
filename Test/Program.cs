using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    internal class Program
    {
        static MLTrader _mlTrader;
        static void Main(string[] args)
        {
            MLTrader.Print += WriteToConsole;

            _mlTrader = new MLTrader();

            using(var myWriter = new StreamWriter(@"C:\Users\MikeSifanele\OneDrive - Optimi\Documents\Data\training data.DAT"))
            {
                StringBuilder states = new StringBuilder();
                StringBuilder labels = new StringBuilder();
                StringBuilder state = new StringBuilder();
                StringBuilder json = new StringBuilder();

                for (var x = 0; x < 30_000; x++)
                {
                    state = new StringBuilder();
                    var obs = _mlTrader.GetState();

                    for (int i = 0; i < _mlTrader.ObservationLength; i++)
                    {
                        state.Append($",[{obs[i].FastEma},{obs[i].SlowEma},{obs[i].High},{obs[i].Low},{obs[i].Close}]");
                    }

                    states.Append($",[{state.ToString().TrimStart(',')}]");

                    if(_mlTrader.RatesSignal == SignalEnum.Neutral)
                        labels.Append($",{(int)_mlTrader.CurrentSignal.Signal}");
                    else
                        labels.Append($",{(int)_mlTrader.RatesSignal}");
                }

                json.Append($"{{\"states\":[{states.ToString().TrimStart(',')}],\"labels\":[{labels.ToString().TrimStart(',')}]}}");

                myWriter.Write(json.ToString());
            }

            Console.WriteLine("Done");

            Console.ReadKey();
        }
        private static void WriteToConsole(string message, bool isSuccess)
        {
            Console.ForegroundColor = isSuccess ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"{DateTime.Now}: {message}");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
    public struct ExpertAction
    {
        public MarketOrderEnum MarketOrder;
        public int? StopLoss;
        public ExpertAction(MarketOrderEnum marketOrder, int stopLoss = 30)
        {
            MarketOrder = marketOrder;
            StopLoss = stopLoss;
        }
    }
    public enum MarketOrderEnum
    {
        Buy,
        Sell,
        Nothing,
        Count
    }
    public enum SignalEnum
    {
        Neutral = 0,
        FastValley = 1,
        SlowValley = 2,
        FastPeak = 3,
        SlowPeak = 4,
        Count
    }
    public static class FibonacciLevels
    {
        /// <summary>
        /// 23.6%
        /// </summary>
        public static float Level1 = .236f;
        /// <summary>
        /// 38.2%
        /// </summary>
        public static float Level2 = .382f;
        /// <summary>
        /// 50%
        /// </summary>
        public static float Level3 = .5f;
        /// <summary>
        /// 61.8%
        /// </summary>
        public static float Level4 = .618f;
        /// <summary>
        /// 76.4%
        /// </summary>
        public static float Level5 = .764f;
    }
    public struct PositionTime
    {
        public DateTime Open;
        public DateTime? Close;
        public PositionTime(string timestamp)
        {
            Open = Convert.ToDateTime(timestamp);
            Close = null;
        }
    }
    public struct CurrentSignal
    {
        public SignalEnum Signal;
        public int Index;
        public CurrentSignal(SignalEnum signal, int index)
        {
            Signal = signal;
            Index = index;
        }
    }
    public class Fibonacci
    {
        public float Level1;
        public float Level2;
        public float Level3;
        public float Level4;
        public float Level5;
    }
    public struct Rates
    {
        public string Time;
        public float Open;
        public float High;
        public float Low;
        public float Close;
        public SignalEnum Signal;
        public float FastEma;
        public float SlowEma;
        public Fibonacci Fibonacci;
        public Rates(string[] data)
        {
            Time = data[0];

            Open = float.Parse(data[1], CultureInfo.InvariantCulture.NumberFormat);
            High = float.Parse(data[2], CultureInfo.InvariantCulture.NumberFormat);
            Low = float.Parse(data[3], CultureInfo.InvariantCulture.NumberFormat);
            Close = float.Parse(data[4], CultureInfo.InvariantCulture.NumberFormat);

            Signal = (SignalEnum)int.Parse(data[5]);

            FastEma = float.Parse(data[6], CultureInfo.InvariantCulture.NumberFormat);
            SlowEma = float.Parse(data[7], CultureInfo.InvariantCulture.NumberFormat);

            Fibonacci = new Fibonacci();
        }
        public float[] ToFloatArray()
        {
            return new float[] { FastEma, SlowEma, Open, High, Low, Close };
        }
    }
    public class Position
    {
        public PositionTime PositionTime;
        public MarketOrderEnum MarketOrder;
        public float OpenPrice;
        public float ClosePrice;
        public int? StopLoss;
        public int Profit;
    }
    public class MLTrader
    {
        #region Private fields
        private Rates[] _rates;
        private readonly int _observationLength = 50;
        private readonly int _startIndex = 240;
        private int _index;
        private int _epoch = 0;
        private float _accumulativeReward = 0;
        private float _maximumReward = 0;
        /// <summary>
        /// Active or current non-neutral signal.
        /// </summary>
        private CurrentSignal _currentSignal;
        private List<Position> _openPositions = new List<Position>();
        private List<Position> _closedPositions = new List<Position>();
        #endregion
        #region Public properties
        public int ObservationLength => _observationLength;
        public int CurrentStepIndex => _index - _startIndex;
        public bool IsLastStep => _index == MaximumRates - 1;
        public int MaximumRates => _rates.Length;
        public float MaximumReward => _maximumReward;
        public float AccumulativeReward => _accumulativeReward;
        public CurrentSignal CurrentSignal => _currentSignal;
        public SignalEnum RatesSignal => _rates[_index].Signal;
        public List<Position> GetOpenPositions => _openPositions;
        public List<Position> GetClosedPositions => _closedPositions;
        #endregion
        #region Delegates
        public delegate void PrintHandler(string message, bool isSuccess);
        public static event PrintHandler Print;
        #endregion
        private static MLTrader _instance;
        public static MLTrader Instance => _instance ?? (_instance = new MLTrader());
        public MLTrader()
        {
            using (var streamReader = new StreamReader(@"C:\Users\MikeSifanele\OneDrive - Optimi\Documents\Data\rates_rates.DAT"))
            {
                List<Rates> rates = new List<Rates>();
                Fibonacci fibonacci = null;

                _ = streamReader.ReadLine();

                int i = 0;
                while (!streamReader.EndOfStream)
                {
                    rates.Add(new Rates(streamReader.ReadLine().Split(',')));

                    if (i >= 239)
                    {
                        fibonacci = CalculateFibonacciLevels(ref rates, i);

                        rates[i].Fibonacci.Level1 = fibonacci.Level1;
                        rates[i].Fibonacci.Level2 = fibonacci.Level2;
                        rates[i].Fibonacci.Level3 = fibonacci.Level3;
                        rates[i].Fibonacci.Level4 = fibonacci.Level4;
                        rates[i].Fibonacci.Level5 = fibonacci.Level5;
                    }

                    i++;
                }

                if (rates[0].Signal > 0)
                    _currentSignal = new CurrentSignal(rates[0].Signal, 0);

                _rates = rates.ToArray();
            }

            Reset();
        }
        public Fibonacci CalculateFibonacciLevels(ref List<Rates> rates, int index)
        {
            try
            {
                float high = 0f;
                float low = 100_000f;

                for (int i = index - 240; i < index; i++)
                {
                    if (rates[i].High > high)
                        high = rates[i].High;

                    if (rates[i].Low < low)
                        low = rates[i].Low;
                }

                float diff = high - low;

                return new Fibonacci()
                {
                    Level1 = high - (diff * FibonacciLevels.Level1),
                    Level2 = high - (diff * FibonacciLevels.Level2),
                    Level3 = high - (diff * FibonacciLevels.Level3),
                    Level4 = high - (diff * FibonacciLevels.Level4),
                    Level5 = high - (diff * FibonacciLevels.Level5),
                };
            }
            catch (Exception)
            {
                return new Fibonacci();
            }
        }
        public float[] GetObservation()
        {
            List<float> observation = new List<float>();

            for (int i = _index - (_observationLength - 1); i <= _index; i++)
            {
                observation.AddRange(_rates[i].ToFloatArray());

                if (_rates[i].Signal != SignalEnum.Neutral)
                    _currentSignal = new CurrentSignal(_rates[i].Signal, i);
            }

            _index++;

            return observation.ToArray();
        }
        public ExpertAction GetExpertAction()
        {
            switch (_currentSignal.Signal)
            {
                case SignalEnum.FastValley:
                    if(GetRisk(MarketOrderEnum.Buy, isExpert: true) < 30)
                        return new ExpertAction(MarketOrderEnum.Buy);
                    break;
                case SignalEnum.SlowValley:
                    if (GetRisk(MarketOrderEnum.Buy, isExpert: true) < 30)
                        return new ExpertAction(MarketOrderEnum.Buy);
                    break;
                case SignalEnum.FastPeak:
                    if (GetRisk(MarketOrderEnum.Sell, isExpert: true) < 30)
                        return new ExpertAction(MarketOrderEnum.Sell);
                    break;
                case SignalEnum.SlowPeak:
                    if (GetRisk(MarketOrderEnum.Sell, isExpert: true) < 30)
                        return new ExpertAction(MarketOrderEnum.Sell);
                    break;
            }

            return new ExpertAction(MarketOrderEnum.Nothing, 0);
        }
        public void OpenPosition(MarketOrderEnum marketOrder, int? stopLoss=null)
        {
            try
            {
                var position = new Position()
                {
                    PositionTime = new PositionTime(_rates[_index].Time),
                    MarketOrder = marketOrder,
                    OpenPrice = _rates[_index].Open,
                    StopLoss = stopLoss
                };

                _openPositions.Add(position);

                Print?.Invoke($"Opened position, Open time: {position.PositionTime.Open}", true);
            }
            catch (Exception)
            {

            }
        }
        public void UpdatePositions()
        {
            try
            {
                for (int i = 0; i < _openPositions.Count; i++)
                {
                    _openPositions[i].Profit = GetPoints(_openPositions[i].MarketOrder, _openPositions[i].OpenPrice, _rates[i].Close) ?? 0;

                    if(_openPositions[i].StopLoss.HasValue)
                        if(_openPositions[i].Profit < -_openPositions[i].StopLoss)
                            ClosePosition(i);
                }
            }
            catch (Exception)
            {

            }
        }
        public void ClosePosition(int index)
        {
            try
            {
                var position = _openPositions[index];

                position.ClosePrice = _rates[_index].Close;
                position.PositionTime.Close = Convert.ToDateTime(_rates[_index].Time);

                AddReward(position.Profit);

                _openPositions.RemoveAt(index);
                _closedPositions.Add(position);

                Print?.Invoke($"Closed position, Open time: {position.PositionTime.Open}, Close time: {position.PositionTime.Close}, Profit: ${FormatNumber(position.Profit)}, Accumulatice reward: ${FormatNumber(_accumulativeReward)}", position.Profit > 0);
            }
            catch (Exception)
            {

            }
        }
        public void ClosePositions()
        {
            try
            {
                for (int i = 0; i < _openPositions.Count; i++)
                {
                    ClosePosition(i);
                }
            }
            catch (Exception)
            {

            }
        }
        public float GetReward(MarketOrderEnum action)
        {
            return GetPoints(action) ?? 0f;
        }
        public void AddReward(float reward)
        {
            _accumulativeReward += reward;

            _maximumReward += reward > 0 ? reward : Math.Abs(reward);
        }
        public int GetRisk(MarketOrderEnum action, bool isExpert = false)
        {
            var index = isExpert ? _currentSignal.Index : _index;
            var openPrice = action == (int)MarketOrderEnum.Buy ? _rates[index].Low : _rates[index].High;

            return GetPoints(action, openPrice, _rates[_index].Open) ?? 0;
        }
        private int? GetPoints(MarketOrderEnum action, float? openPrice = null, float? closePrice = null)
        {
            openPrice = openPrice ?? _rates[_index].Open;
            closePrice = closePrice ?? _rates[_index].Close;

            int? points = 0;

            if (action == MarketOrderEnum.Buy)
                points = (int?)((closePrice - openPrice) * 10);
            else if (action == MarketOrderEnum.Sell)
                points = (int?)((openPrice - closePrice) * 10);

            if (action == MarketOrderEnum.Nothing)
                points = points > 0 ? -points : points;

            return points;
        }
        public void Reset()
        {
            _epoch++;
            _accumulativeReward = 0;
            _index = _startIndex;
        }
        public string GetReport()
        {
            var rewardString = FormatNumber(_accumulativeReward);
            var maximumRewardString = FormatNumber(_maximumReward);

            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"Episode ended: {_epoch}\n");
            stringBuilder.AppendLine($"Reward: ${rewardString}/${maximumRewardString}\n");
            stringBuilder.AppendLine($"Accuracy: {FormatNumber(_accumulativeReward / _maximumReward * 100)}%\n");
            stringBuilder.AppendLine($"Total trades: {_closedPositions.Count}");
            stringBuilder.AppendLine($"Trades won: {_closedPositions.Where(x => x.Profit > 0).Count()}");
            stringBuilder.AppendLine($"Trades lost: {_closedPositions.Where(x => x.Profit <= 0).Count()}");
            stringBuilder.AppendLine($"Maximum profit: ${FormatNumber(_closedPositions.Max(x => x.Profit))}");
            stringBuilder.AppendLine($"Maximum drawdown: ${FormatNumber(_closedPositions.Min(x => x.Profit))}");

            return stringBuilder.ToString();
        }
        public string FormatNumber(float value)
        {
            return value.ToString("N", CultureInfo.CreateSpecificCulture("sv-SE"));
        }
        public string FormatNumber(int value)
        {
            return value.ToString("N", CultureInfo.CreateSpecificCulture("sv-SE"));
        }
    }
}
