#include"levelbond.h"
#include<math.h>
using namespace std;

levelbond::levelbond(double c, double p, double m){ 
	coupon = c / 100 / frequency;
	rawcoupon=c;
	paymentnum = m*frequency;
	 price = p*10; 
	 maturity = m; }
double levelbond::expression_value(double ytm_tester)
{
	double bondvalue = 0;
	double den = 1.0 + ytm_tester / frequency;
	for (int i = 0; i <paymentnum; i++)
	{
		bondvalue += coupon*facevalue / pow(den, i + 1);
	}
	bondvalue += facevalue / pow(den, paymentnum);
	double expression = (bondvalue - price);
	return expression;
}
double levelbond::derivative(double root_tester)
{
	double expression_increase = expression_value(root_tester + mini) - expression_value(root_tester);
	double root_increase = mini;
	double deriv = expression_increase / root_increase;
	return deriv;
}
int  levelbond::check(double a, double b){
	double m = abs(a - b);
	if (m <= epsi)
		return 1;
	else
		return 0;

}
double levelbond::mainexpression_value(double previous_value, double ytm_tester)
{
	double value = 0;
	double den = 1.0 + ytm_tester / frequency;
	value = previous_value + (facevalue + coupon*facevalue) / pow(den, paymentnum);// don't use coupon but coupon*facevalue!!!!!
	double expression = (value - price);
	return expression;
}
double levelbond::mainderivative(double previous_value, double root_tester)
{

	double expression_increase = mainexpression_value(previous_value, root_tester + mini) - mainexpression_value(previous_value, root_tester);
	double root_increase = mini;
	double deriv = expression_increase / root_increase;
	return deriv;

}
void levelbond::vtmcalculator()
{
	while (1)
	{
		ytm = ytm0 - expression_value(ytm0) / derivative(ytm0);
		if (check(ytm, ytm0))
			break;
		ytm0 = ytm;
	}
}
void levelbond::calculator(double previous_value){

	while (1)
	{
		spot_rate = r0 - mainexpression_value(previous_value,r0) / mainderivative(previous_value,r0);
		if (check(spot_rate, r0))
			break;
		r0 = spot_rate;
	}


}

double levelbond::get_coupon(){ return coupon;}
double levelbond::get_maturity(){ return maturity; }
double levelbond::get_price(){ return price;}
