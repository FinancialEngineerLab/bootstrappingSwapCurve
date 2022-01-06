#include <iostream>
#include <fstream>
#include <vector>
#include <iomanip>
#include "utilities.hpp"
#include "curvebuilder.hpp"

using namespace std;

int main()
{
	ifstream infile;
	infile.open("data.txt");
	double x;
	int mark = 1;
	vector<curveUtils> bondSeries;
	vector<curveUtils>::iterator bondSeries_iter = bondSeries.begin();
	vector<double> parameters;
	while (mark)
	{
		infile >> x;
		if (infile)
		{
			parameters.push_back(x);
		}
		else
		{
			mark = 0;
			if (infile.eof())
			{
				cout << endl;
				cout << '\t' << '\t' << '\t' << " --- *** End of the file ***---" << endl;
			}
			else
			{
				cout << "Wrong !! " << endl;
			}
		}
	}
	for (int i = 0, j = 0; i < parameters.size() / 3; i++)
	{
		curveUtils temp(parameters[j], parameters[j + 1], parameters[j + 2]);
		bondSeries.push_back(temp);
		j += 3;
	}

	// Constructing a complete term //
	for (bondSeries_iter = bondSeries.begin(); bondSeries_iter != bondSeries.end() - 1; bondSeries_iter++)
	{
		double m = ((*(bondSeries_iter + 1)).get_maturity() - (*(bondSeries_iter)).get_maturity()) / 0.5 - 1.0;
		if (m > 0)
		{
			double insert_coupon = (*(bondSeries_iter)).get_rawcoupon() + ((*(bondSeries_iter + 1)).get_rawcoupon() - (*(bondSeries_iter)).get_rawcoupon()) / (m + 1);
			double insert_maturtiy = (*bondSeries_iter).get_maturity() + 0.5;
			curveUtils insertBond(insert_coupon, 100, insert_maturtiy);
			bondSeries.insert((bondSeries_iter + 1), insertBond);
			bondSeries_iter = bondSeries.begin();
		}
	}
	curveBuilder calculator(bondSeries);
	calculator.spot_rate();
	calculator.discount_factor();
	calculator.forward();

	vector<double> spot_rate = calculator.get_result();
	vector<double> discount = calculator.get_discount();
	vector<double> forward = calculator.get_forward();
	vector<double>::iterator it_1 = spot_rate.begin();
	vector<double>::iterator it_2 = discount.begin();
	vector<double>::iterator it_3 = forward.begin();
	cout << endl;

	cout << "Tenor (M)" << "\t" << "Spot_Rate (%) " << "\t" << "\t" << "DF (%) " << "\t " << "Forwad (%) " << endl;
	int m = 1;
	for (; it_1 != spot_rate.end(); it_1++, it_2++, it_3++, ++m)
	{
		cout << setprecision(5) << m * 6 << "\t" << "\t" << "\t" << (*it_1) * 100 << "\t" << "\t" << "\t" << (*it_2) << "\t" << "\t" << "\t" << (*it_3) << endl;
	}

	return 1;
}