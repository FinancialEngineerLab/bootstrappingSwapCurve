using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ExcelDna.Integration;

namespace CurveAddin
{
    public static class CurveAddin
    {
        public static void CreateUSDLiborCurves()
        {
            try
            {
                // build market data from Excel worksheet
                MarketDataBuilder builder = new ExcelBuilder();
                CurveData marketData = builder.Build();
                DateTime settlementDate = ((ExcelBuilder)builder).SettlementDate;
                //
                // construct USD Libor curve object
                // HARD-CODED parameters : 
                // interpolation method, date conventions, number of contracts for cash (n=1) and futures (n=3)
                ICurve curve = new USDLiborZeroCurve(marketData, Interpolation.LinearInterpolation, 
                    DateConvention.AddPeriod_ModifiedFollowing, DateConvention.ACT360, settlementDate, 1, 3);
                curve.Create();
                //
                // read discount factor and forward rate data into 2d-array
                int rows = curve.DiscountCurve.Count();
                int cols = 3;
                dynamic[,] data = new dynamic[rows, cols];
                for (int i = 0; i < rows; i++)
                {
                    data[i, 0] = curve.DiscountCurve[i].MaturityDate.ToOADate();
                    data[i, 1] = curve.DiscountCurve[i].Rate;
                    data[i, 2] = curve.ForwardCurve[i].Rate;
                }
                //
                // print curve data into Excel worksheet
                (new ExcelPrinter(data)).Print();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}
