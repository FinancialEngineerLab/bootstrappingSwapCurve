#ifndef LEVELBOND_H
#define LEVELBOND_H
#include <iostream>

class curveUtils
{
public:
	curveUtils(double c, double p, double m)
	{
		coupon = c / 100.0 / frequency;
		rawcoupon = c;
		paymentnum = m * frequency;
		price = p * 10;
		maturity = m;
	}

	int check(double a, double b)
	{
		double m = abs(a - b);
		if (m <= epsi)
		{
			return 1;
		}
		else
		{
			return 0;
		}
	}


	double expression_value(double ytm_tester)
	{
		double bondvalue = 0.0;
		double den = 1.0 + ytm_tester / frequency;
		for (int i = 0; i < paymentnum; i++)
		{
			bondvalue += coupon * facevalue / pow(den, i + 1);
		}

		double expression = (bondvalue - price);
		return expression;
	}

	double derivative(double root_tester)
	{
		double expression_increase = expression_value(root_tester + mini) - expression_value(root_tester);
		double root_increase = mini;
		double deriv = expression_increase / root_increase; // difference per mini
		return deriv;
	}

	// main : previous ! 
	double main_expression_value(double previous_num, double ytm_tester)
	{
		double expression_increase = main_expression_value(previous_num, ytm_tester + mini) - main_expression_value(previous_num, ytm_tester);
		double root_increase = mini;
		double deriv = expression_increase / root_increase; // difference per mini
		return deriv;

	}
	double main_derivative(double previous_num, double root_tester)
	{
		double expression_increase = main_expression_value(previous_num, root_tester + mini) - main_expression_value(previous_num, root_tester);
		double root_increase = mini;
		double deriv = expression_increase / root_increase; // difference per mini
		return deriv;
	}


	void ytmcalculator()
	{
		while (1)
		{
			ytm = ytm0 - expression_value(ytm0) / derivative(ytm0);
			if (check(ytm, ytm0))
			{
				break;
			}
			ytm0 = ytm;
		}
	}
	void calculator(double previous_value)
	{
		while (1)
		{
			spot_rate = r0 - main_expression_value(previous_value, r0) / main_derivative(previous_value, r0);
			if (check(spot_rate, r0))
			{
				break;
			}
			r0 = spot_rate;
		}
	}

	

	double get_ytm() { return ytm; };
	double get_spot() { return spot_rate; };
	double get_paymentnum() { return paymentnum; };
	double get_rawcoupon() { return rawcoupon; };
	double get_coupon() { return coupon; };
	double get_maturity() { return maturity; };
	double get_price() { return price; };

private:
	double rawcoupon;
	double coupon;
	double price;
	double maturity;
	double paymentnum;
	double facevalue = 100000000;
	double frequency = 4;
	double epsi = 0.0000000000001;
	double mini = 0.0000000000001;
	double ytm0 = 0.013;
	double r0 = 0.013;
	double ytm;
	double spot_rate;
};



#endif