#ifndef LEVELBOND_H
#define LEVELBOND_H
#include<iostream>
class levelbond
{
public:
	levelbond (double c, double p, double m);
	double get_coupon();
	double get_price();
	double get_maturity();
	int check(double a, double b);
	double expression_value(double tester);
	double mainexpression_value(double previous_num,double ytm_tester);
	double levelbond::mainderivative(double previous_value, double root_tester);
	double derivative(double tester);
	void levelbond::vtmcalculator();
	void levelbond::calculator(double previous_value);
	double get_ytm(){ return ytm; };
	double get_spot(){ return spot_rate; };
	double get_paymentnum(){ return paymentnum; };
	double get_rawcoupon(){ return rawcoupon; };


private:
	double rawcoupon;
	double coupon;
	double price;
	double maturity;
	double paymentnum;
	double facevalue = 1000;
	double frequency = 2;
	double epsi = 0.0000001;
	double mini = 0.000001;
	double ytm0 = 0.256988;
	double r0 = 0.2569;
	double ytm;
	double spot_rate;

};

#endif