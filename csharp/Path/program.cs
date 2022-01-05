using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExcelDna.Integration;

namespace CurveAddin
{
    //
    // public enumerators and delegate methods
    public enum ENUM_MARKETDATATYPE { CASH = 1, FRA = 2, FUTURE = 3, SWAP = 4, DF = 5, SPOTRATE = 6, FORWARDRATE = 7 }
    public enum ENUM_PERIODTYPE { MONTHS = 1, YEARS = 2 }
    public delegate double DayCountFactor(DateTime start, DateTime end);
    public delegate DateTime AddPeriod(DateTime start, ENUM_PERIODTYPE periodType, int period);
    public delegate double Interpolator(DayCountFactor dayCountFactor, Dictionary<DateTime, double> data, DateTime key);
    public delegate double ConvexityAdjustment(double rateVolatility, double start, double end);
    //
    //
    // class hierarchy for curve printer
    public abstract class CurvePrinter
    {
        public abstract void Print();
    }
    public class ExcelPrinter : CurvePrinter
    {
        private static dynamic Excel;
        private dynamic[,] data;
        public ExcelPrinter(dynamic[,] data)
        {
            this.data = data;
        }
        public override void Print()
        {
            // Create Excel application
            Excel = ExcelDnaUtil.Application;
            //
            // clear old data from output range, resize output range 
            // and finally print data to Excel worksheet
            Excel.Range["_USDLiborZeroCurve"].CurrentRegion = "";
            Excel.Range["_USDLiborZeroCurve"].Resize[data.GetLength(0), data.GetLength(1)] = data;
        }
    }
    //
    //
    // class hierarchy for market data builder
    public abstract class MarketDataBuilder
    {
        public abstract CurveData Build();
    }
    public class ExcelBuilder : MarketDataBuilder
    {
        private static dynamic Excel;
        private DateTime settlementDate;
        public DateTime SettlementDate { get { return this.settlementDate; } }
        //
        public override CurveData Build()
        {
            // Create Excel application
            Excel = ExcelDnaUtil.Application;
            //
            // read settlement date from Excel worksheet
            settlementDate = DateTime.FromOADate((double)Excel.Range("_settlementDate").Value2);
            //
            // read source security data from Excel worksheet
            object[,] ExcelSourceData = (object[,])Excel.Range["_marketData"].CurrentRegion.Value2;
            //
            // create curve data object from source security data
            CurveData marketData = new CurveData(Interpolation.LinearInterpolation);
            int rows = ExcelSourceData.GetUpperBound(0);
            for (int i = 1; i <= rows; i++)
            {
                DateTime maturity = DateTime.FromOADate((double)ExcelSourceData[i, 1]);
                double rate = (double)ExcelSourceData[i, 2];
                string instrumentType = ((string)ExcelSourceData[i, 3]).ToUpper();
                ENUM_MARKETDATATYPE marketDataType = (ENUM_MARKETDATATYPE)Enum.Parse(typeof(ENUM_MARKETDATATYPE), instrumentType);
                marketData.AddCurveDataElement(maturity, rate, marketDataType);
            }
            return marketData;
        }
    }
    //
    //
    // interface for all curve objects
    public interface ICurve
    {
        void Create();
        double GetDF(DateTime maturity);
        double GetFWD(DateTime start);
        Dictionary<DateTime, double> GetDF(DateTime start, int nYears);
        Dictionary<DateTime, double> GetFWD(DateTime start, int nYears);
        CurveData DiscountCurve { get; }
        CurveData ForwardCurve { get; }
    }
    //
    // implementation for USD Libor curve
    public class USDLiborZeroCurve : ICurve
    {
        public readonly DayCountFactor dayCountFactor;
        public readonly AddPeriod addPeriod;
        public readonly int basis;
        public readonly Interpolator interpolator;
        public readonly DateTime settlementDate;
        public CurveData DiscountCurve { get { return this.discountCurve; } }
        public CurveData ForwardCurve { get { return this.forwardCurve; } }
        //
        private CurveData marketData;
        private CurveData curveDataSelection;
        private CurveData bootstrapCurve;
        private CurveData spotCurve;
        private CurveData discountCurve;
        private CurveData forwardCurve;
        private int nCash;
        private int nFuturesOrFRAs;
        private bool adjustmentForConvexity;
        private ConvexityAdjustment convexityAdjustment;
        private double rateVolatility;
        //
        public USDLiborZeroCurve(CurveData marketData, Interpolator interpolator, AddPeriod addPeriod,
            DayCountFactor dayCountFactor, DateTime settlementDate, int nCash, int nFuturesOrFRAs,
            bool adjustmentForConvexity = false, ConvexityAdjustment convexityAdjustment = null, double rateVolatility = 0.0)
        {
            this.marketData = marketData;
            this.interpolator = interpolator;
            this.addPeriod = addPeriod;
            this.dayCountFactor = dayCountFactor;
            this.settlementDate = settlementDate;
            this.nCash = nCash;
            this.nFuturesOrFRAs = nFuturesOrFRAs;
            this.basis = 3; // HARD-CODED !! for USD Libor curve
            this.adjustmentForConvexity = adjustmentForConvexity; // optional parameter
            this.convexityAdjustment = convexityAdjustment; // optional parameter
            this.rateVolatility = rateVolatility; // optional parameter
        }
        public void Create()
        {
            // sequence of private methods for creating spot discount curve and
            // simply-compounded forward rate curve for a given set of market data
            checkMarketData();
            selectCurveData();
            bootstrapDiscountFactors();
            createSpotCurve();
            createDiscountCurve();
            createForwardCurve();
        }
        // get discount factor for a given maturity date
        public double GetDF(DateTime maturity)
        {
            return discountCurve.GetMarketRate(ENUM_MARKETDATATYPE.DF, maturity, dayCountFactor);
        }
        // get dictionary consisting of date and discount factor for a date schedule
        public Dictionary<DateTime, double> GetDF(DateTime start, int nYears)
        {
            List<DateTime> schedule = DateConvention.CreateDateSchedule(start, nYears, basis, ENUM_PERIODTYPE.MONTHS, addPeriod);
            Dictionary<DateTime, double> curve = new Dictionary<DateTime, double>();
            schedule.ForEach(it => curve.Add(it, GetDF(it)));
            return curve;
        }
        // get simply-compounded forward rate for a given start date
        public double GetFWD(DateTime start)
        {
            return forwardCurve.GetMarketRate(ENUM_MARKETDATATYPE.FORWARDRATE, start, dayCountFactor);
        }
        // get dictionary consisting of date and simply-compounded forward rate for a date schedule
        public Dictionary<DateTime, double> GetFWD(DateTime start, int nYears)
        {
            List<DateTime> schedule = DateConvention.CreateDateSchedule(start, nYears, basis, ENUM_PERIODTYPE.MONTHS, addPeriod);
            Dictionary<DateTime, double> curve = new Dictionary<DateTime, double>();
            schedule.ForEach(it => curve.Add(it, GetFWD(it)));
            return curve;
        }
        // use interpolated spot discount factor curve for calculating
        // simply-compounded forward rates for all required maturities (basis)
        // note : maturity element of the forward curve stores the information
        // on when the 3-month period starts for a given forward rate element
        private void createForwardCurve()
        {
            forwardCurve = new CurveData(interpolator);
            int n = discountCurve.Count();
            DateTime maturity;
            double dt = 0.0;
            double fdf = 0.0;
            double f = 0.0;
            //
            for (int i = 0; i < n; i++)
            {
                if (i == 0)
                {
                    // first forward rate is the first spot rate
                    maturity = discountCurve[i].MaturityDate;
                    fdf = discountCurve[i].Rate;
                    dt = dayCountFactor(settlementDate, maturity);
                    f = ((1 / fdf) - 1) / dt;
                    forwardCurve.AddCurveDataElement(settlementDate, f, ENUM_MARKETDATATYPE.FORWARDRATE);
                }
                else
                {
                    // other forward rates are calculated recursively
                    // from previous spot discount factors
                    maturity = discountCurve[i].MaturityDate;
                    DateTime previousMaturity = discountCurve[i - 1].MaturityDate;
                    fdf = discountCurve[i].Rate / discountCurve[i - 1].Rate;
                    dt = dayCountFactor(previousMaturity, maturity);
                    f = ((1 / fdf) - 1) / dt;
                    forwardCurve.AddCurveDataElement(previousMaturity, f, ENUM_MARKETDATATYPE.FORWARDRATE);
                }
            }
        }
        // use continuously compounded spot rate curve for interpolating 
        // continuously compounded spot rates for all required maturities 
        // and convert these spot rates back to spot discount factors
        private void createDiscountCurve()
        {
            discountCurve = new CurveData(interpolator);
            DateTime finalCurveDate = spotCurve.ElementAt(spotCurve.Count() - 1).MaturityDate;
            DateTime t;
            int counter = 0;
            double dt = 0.0;
            double r = 0.0;
            double df = 0.0;
            //
            do
            {
                counter++;
                t = addPeriod(settlementDate, ENUM_PERIODTYPE.MONTHS, basis * counter);
                dt = dayCountFactor(settlementDate, t);
                r = spotCurve.GetMarketRate(ENUM_MARKETDATATYPE.SPOTRATE, t, dayCountFactor);
                df = Math.Exp(-r * dt);
                discountCurve.AddCurveDataElement(t, df, ENUM_MARKETDATATYPE.DF);
            } while (t < finalCurveDate);
        }
        // create continuously compounded spot rate curve 
        // from bootstrapped discount factors
        private void createSpotCurve()
        {
            spotCurve = new CurveData(interpolator);
            double t = 0.0;
            double r = 0.0;
            int n = bootstrapCurve.Count();
            for (int i = 0; i < n; i++)
            {
                t = dayCountFactor(settlementDate, bootstrapCurve.ElementAt(i).MaturityDate);
                r = -Math.Log(bootstrapCurve.ElementAt(i).Rate) / t;
                spotCurve.AddCurveDataElement(bootstrapCurve.ElementAt(i).MaturityDate, r, ENUM_MARKETDATATYPE.SPOTRATE);
            }
        }
        // use bootstrap algorithm to create spot discount factors
        // from all selected curve data elements
        private void bootstrapDiscountFactors()
        {
            bootstrapCurve = new CurveData(interpolator);
            double dt = 0.0;
            double r = 0.0;
            double df = 0.0;
            double Q = 0.0;
            int n = curveDataSelection.Count();
            //
            for (int i = 0; i < n; i++)
            {
                if (curveDataSelection[i].InstrumentType == ENUM_MARKETDATATYPE.CASH)
                {
                    dt = dayCountFactor(settlementDate, curveDataSelection[i].MaturityDate);
                    r = curveDataSelection[i].Rate;
                    df = 1 / (1 + r * dt);
                    bootstrapCurve.AddCurveDataElement(curveDataSelection[i].MaturityDate, df, ENUM_MARKETDATATYPE.DF);
                }
                if ((curveDataSelection[i].InstrumentType == ENUM_MARKETDATATYPE.FRA) |
                    (curveDataSelection[i].InstrumentType == ENUM_MARKETDATATYPE.FUTURE))
                {
                    dt = dayCountFactor(curveDataSelection[i - 1].MaturityDate, curveDataSelection[i].MaturityDate);
                    r = curveDataSelection[i].Rate;
                    df = bootstrapCurve.ElementAt(i - 1).Rate / (1 + r * dt);
                    bootstrapCurve.AddCurveDataElement(curveDataSelection[i].MaturityDate, df, ENUM_MARKETDATATYPE.DF);
                    //
                    if ((curveDataSelection[i + 1].InstrumentType == ENUM_MARKETDATATYPE.SWAP))
                        Q += bootstrapCurve.ElementAt(i).Rate * dayCountFactor(settlementDate, curveDataSelection[i].MaturityDate);
                }
                //
                if (curveDataSelection[i].InstrumentType == ENUM_MARKETDATATYPE.SWAP)
                {
                    r = curveDataSelection[i].Rate;
                    dt = dayCountFactor(bootstrapCurve.ElementAt(i - 1).MaturityDate, curveDataSelection[i].MaturityDate);
                    df = (1 - r * Q) / (r * dt + 1);
                    bootstrapCurve.AddCurveDataElement(curveDataSelection[i].MaturityDate, df, ENUM_MARKETDATATYPE.DF);
                    Q += (df * dt);
                }
            }
        }
        // select rate instruments to be used from a given set of curve data elements
        private void selectCurveData()
        {
            curveDataSelection = new CurveData(interpolator);
            int counter = 0;
            double rate = 0.0;
            DateTime maturityDate;
            //
            // select cash securities
            for (int i = 1; i <= nCash; i++)
            {
                counter++;
                maturityDate = addPeriod(settlementDate, ENUM_PERIODTYPE.MONTHS, basis * counter);
                // check if cash rate for required maturity exists
                if (!marketData.ElementLookup(ENUM_MARKETDATATYPE.CASH, maturityDate))
                    throw new Exception("USDLiborZeroCurve error : required cash securities are missing");
                rate = marketData.GetMarketRate(ENUM_MARKETDATATYPE.CASH, maturityDate, dayCountFactor);
                curveDataSelection.AddCurveDataElement(maturityDate, rate, ENUM_MARKETDATATYPE.CASH);
            }
            // select fra or futures contracts
            if (marketData.ElementLookup(ENUM_MARKETDATATYPE.FRA))
            {
                for (int i = 1; i <= nFuturesOrFRAs; i++)
                {
                    if (i > 1) counter++;
                    maturityDate = addPeriod(settlementDate, ENUM_PERIODTYPE.MONTHS, basis * counter);
                    // check if fra rate for required maturity exists
                    if (!marketData.ElementLookup(ENUM_MARKETDATATYPE.FRA, maturityDate))
                        throw new Exception("USDLiborZeroCurve error : required FRA contracts are missing");
                    rate = marketData.GetMarketRate(ENUM_MARKETDATATYPE.FRA, maturityDate, dayCountFactor);
                    curveDataSelection.AddCurveDataElement(addPeriod(maturityDate, ENUM_PERIODTYPE.MONTHS, basis), rate, ENUM_MARKETDATATYPE.FRA);
                }
            }
            else
            {
                for (int i = 1; i <= nFuturesOrFRAs; i++)
                {
                    if (i > 1) counter++;
                    maturityDate = addPeriod(settlementDate, ENUM_PERIODTYPE.MONTHS, basis * counter);
                    // check if implied futures rate for required maturity exists
                    if (!marketData.ElementLookup(ENUM_MARKETDATATYPE.FUTURE, maturityDate))
                        throw new Exception("USDLiborZeroCurve error : required futures contracts are missing");
                    rate = marketData.GetMarketRate(ENUM_MARKETDATATYPE.FUTURE, maturityDate, dayCountFactor);
                    //
                    // forward rate = futures rate - convexity adjustment
                    if (adjustmentForConvexity)
                    {
                        double t1 = dayCountFactor(settlementDate, maturityDate);
                        double t2 = t1 + (basis / 12.0);
                        rate -= convexityAdjustment(rateVolatility, t1, t2);
                    }
                    curveDataSelection.AddCurveDataElement(addPeriod(maturityDate, ENUM_PERIODTYPE.MONTHS, basis), rate, ENUM_MARKETDATATYPE.FUTURE);
                }
            }
            // select swap contracts
            DateTime lastSwapYear = marketData[marketData.Count() - 1].MaturityDate;
            DateTime lastFRAOrFutureYear = curveDataSelection[curveDataSelection.Count() - 1].MaturityDate;
            int nSwaps = (lastSwapYear.Year - lastFRAOrFutureYear.Year);
            for (int i = 1; i <= nSwaps; i++)
            {
                counter++;
                maturityDate = addPeriod(settlementDate, ENUM_PERIODTYPE.YEARS, i + 1);
                // check if swap rate for required maturity exists
                if (!marketData.ElementLookup(ENUM_MARKETDATATYPE.SWAP, maturityDate))
                    throw new Exception("USDLiborZeroCurve error : required swap contracts are missing");
                rate = marketData.GetMarketRate(ENUM_MARKETDATATYPE.SWAP, maturityDate, dayCountFactor);
                curveDataSelection.AddCurveDataElement(maturityDate, rate, ENUM_MARKETDATATYPE.SWAP);
            }
        }
        // rough diagnostics : check for completely non-existing market data
        // requirement : all three rate categories (cash, FRA/futures, swaps) 
        // must be provided by the client in order to create the curves
        private void checkMarketData()
        {
            // cash securities
            if (!marketData.ElementLookup(ENUM_MARKETDATATYPE.CASH))
                throw new Exception("LiborZeroCurve error : cash securities are required to build the curve");
            //
            // fra/futures contracts
            if ((!marketData.ElementLookup(ENUM_MARKETDATATYPE.FUTURE)) && (!marketData.ElementLookup(ENUM_MARKETDATATYPE.FRA)))
                throw new Exception("LiborZeroCurve error : FRA or futures contracts are required to build the curve");
            //
            // swap contracts
            if (!marketData.ElementLookup(ENUM_MARKETDATATYPE.SWAP))
                throw new Exception("LiborZeroCurve error : swap contracts are required to build the curve");
        }
    }
    //
    //
    // container class for holding multiple curve data elements
    public class CurveData : IEnumerable<CurveDataElement>
    {
        private List<CurveDataElement> curveDataElements;
        private Interpolator interpolator;
        //
        public CurveData(Interpolator interpolator)
        {
            this.interpolator = interpolator;
            curveDataElements = new List<CurveDataElement>();
        }
        public void AddCurveDataElement(DateTime maturity, double rate, ENUM_MARKETDATATYPE instrumentType)
        {
            curveDataElements.Add(new CurveDataElement(maturity, rate, instrumentType));
        }
        public void AddCurveDataElement(CurveDataElement curveDataElement)
        {
            curveDataElements.Add(curveDataElement);
        }
        // implementation for generic IEnumerable
        public IEnumerator<CurveDataElement> GetEnumerator()
        {
            foreach (CurveDataElement curveDataElement in curveDataElements)
            {
                yield return curveDataElement;
            }
        }
        // implementation for non-generic IEnumerable
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        // read-only indexer
        public CurveDataElement this[int index]
        {
            get
            {
                return curveDataElements[index];
            }
        }
        public double GetMarketRate(ENUM_MARKETDATATYPE instrumentType, DateTime maturity, DayCountFactor dayCountFactor)
        {
            // filter required market data elements by instrument type
            List<CurveDataElement> group = curveDataElements.Where(it => it.InstrumentType == instrumentType).ToList<CurveDataElement>();
            //
            // extract maturity and rate into dictionary object
            Dictionary<DateTime, double> data = new Dictionary<DateTime, double>();
            group.ForEach(it => data.Add(it.MaturityDate, it.Rate));
            //
            // get market rate for a given date by using given interpolation delegate method
            return interpolator(dayCountFactor, data, maturity);
        }
        // check if elements with specific instrument type and maturity exists
        public bool ElementLookup(ENUM_MARKETDATATYPE instrumentType, DateTime maturity)
        {
            // first, filter required market data elements
            List<CurveDataElement> group = curveDataElements.Where(it => it.InstrumentType == instrumentType).ToList<CurveDataElement>();
            //
            // then, check if maturity lies between min and max maturity of filtered group
            bool hasElement = ((maturity >= group.Min(it => it.MaturityDate)) && (maturity <= group.Max(it => it.MaturityDate)));
            return hasElement;
        }
        // check if elements with only specific instrument type exists
        public bool ElementLookup(ENUM_MARKETDATATYPE instrumentType)
        {
            int elements = curveDataElements.Count(it => it.InstrumentType == instrumentType);
            bool hasElements = false;
            if (elements > 0) hasElements = true;
            return hasElements;
        }
    }
    //
    //
    // class holding information on one curve data element
    public class CurveDataElement
    {
        private DateTime maturityDate;
        private double rate;
        private ENUM_MARKETDATATYPE rateType;
        //
        public DateTime MaturityDate { get { return this.maturityDate; } }
        public double Rate { get { return this.rate; } }
        public ENUM_MARKETDATATYPE InstrumentType { get { return this.rateType; } }
        //
        public CurveDataElement(DateTime maturity, double rate, ENUM_MARKETDATATYPE rateType)
        {
            this.maturityDate = maturity;
            this.rate = rate;
            this.rateType = rateType;
        }
    }
    //
    //
    // static library class for handling date-related convention calculations
    public static class DateConvention
    {
        // calculate time difference between two dates by using ACT/360 convention
        public static double ACT360(DateTime start, DateTime end)
        {
            return (end - start).TotalDays / 360;
        }
        // create a list of scheduled dates for a given basis and date convention
        public static List<DateTime> CreateDateSchedule(DateTime start, int nYears, int basis,
            ENUM_PERIODTYPE periodType, AddPeriod addPeriod)
        {
            List<DateTime> schedule = new List<DateTime>();
            int nPeriods = nYears * (12 / basis);
            for (int i = 1; i <= nPeriods; i++)
            {
                schedule.Add(addPeriod(start, periodType, (basis * i)));
            }
            return schedule;
        }
        // add period into a given date by using modified following convention
        public static DateTime AddPeriod_ModifiedFollowing(DateTime start, ENUM_PERIODTYPE periodType, int period)
        {
            DateTime dt = new DateTime();
            //
            switch (periodType)
            {
                case ENUM_PERIODTYPE.MONTHS:
                    dt = start.AddMonths(period);
                    break;
                case ENUM_PERIODTYPE.YEARS:
                    dt = start.AddYears(period);
                    break;
            }
            //
            switch (dt.DayOfWeek)
            {
                case DayOfWeek.Saturday:
                    dt = dt.AddDays(2.0);
                    break;
                case DayOfWeek.Sunday:
                    dt = dt.AddDays(1.0);
                    break;
            }
            return dt;
        }
        // calculate value for convexity adjustment for a given time period
        public static double SimpleConvexityApproximation(double rateVolatility, double start, double end)
        {
            return 0.5 * (rateVolatility * rateVolatility * start * end);
        }
    }
    //
    //
    // static library class for storing interpolation methods to be used by delegates
    public static class Interpolation
    {
        public static double LinearInterpolation(DayCountFactor dayCountFactor,
            Dictionary<DateTime, double> data, DateTime key)
        {
            double value = 0.0;
            int n = data.Count;
            //
            // boundary checkings
            if ((key < data.ElementAt(0).Key) || (key > data.ElementAt(data.Count - 1).Key))
            {
                if (key < data.ElementAt(0).Key) throw new Exception("Interpolation error : lower bound violation");
                if (key > data.ElementAt(data.Count - 1).Key) throw new Exception("Interpolation error : upper bound violation");
            }
            else
            {
                // iteration through all existing elements
                for (int i = 0; i < n; i++)
                {
                    if ((key >= data.ElementAt(i).Key) && (key <= data.ElementAt(i + 1).Key))
                    {
                        double t = dayCountFactor(data.ElementAt(i).Key, data.ElementAt(i + 1).Key);
                        double w = dayCountFactor(data.ElementAt(i).Key, key) / t;
                        value = data.ElementAt(i).Value * (1 - w) + data.ElementAt(i + 1).Value * w;
                        break;
                    }
                }
            }
            return value;
        }
    }
}
