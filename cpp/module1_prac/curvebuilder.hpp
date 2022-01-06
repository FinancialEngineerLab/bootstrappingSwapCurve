#ifndef CURVEBUILDER_HPP
#define CURVEBUILDER_HPP

#include <vector>
#include "utilities.hpp"

using namespace std;

class curveBuilder
{
public:

	curveBuilder(vector<curveUtils> input)
	{
		level_term = input;
	}

	void spot_rate()
	{
		vector<curveUtils>::iterator iter = level_term.begin();

		(*iter).ytmcalculator();
		spot_termStructure.push_back(((*iter).get_ytm() + 0.00000));
		double p = 1.0 / (1.0 + (*iter).get_ytm() / 2.0);
		iter++; // pointing to following terms
		for (; iter != level_term.end(); iter++)
		{
			(*iter).calculator(((*iter).get_coupon()) * 1000 * p);
			spot_termStructure.push_back((*iter).get_spot());
			double den = 1.0 + (*iter).get_spot() / 2.0;
			p += 1.0 / pow(den, (*iter).get_paymentnum());
		}
	}
	void discount_factor()
	{
		vector<double>::iterator t = spot_termStructure.begin();
		double times = 0.0;
		for (; t != spot_termStructure.end(); t++)
		{
			double temp2 = pow(1.0 + (*t) / 2.0, -(times + 1.0) / 2.0);
			double temp = 1 / pow((1 + (*t)), times);
			discount_termStructure.push_back(temp2);
			times++;
		}
	}
	void forward()
	{
		for (int i = 0; i < spot_termStructure.size() - 1; ++i)
		{
			double temp = (pow((pow(1.0 + spot_termStructure[i + 1] / 2.0, (i + 2) / 2.0) / pow(1.0 + spot_termStructure[i] / 2.0, (i + 1) / 2.0)), 1.0 / 0.5) - 1.0)* 100.0 * 2.0;
			forward_termStructure.push_back(temp);
		}
		forward_termStructure.push_back(0.0000);
	}

	vector<double> get_result()
	{
		return spot_termStructure;
	};
	vector<double> get_discount()
	{
		return discount_termStructure;
	};
	vector<double> get_forward()
	{
		return forward_termStructure;
	};

private:
	vector<double> spot_termStructure;
	vector<double> discount_termStructure;
	vector<double> forward_termStructure;
	vector<curveUtils> level_term;
};

#endif