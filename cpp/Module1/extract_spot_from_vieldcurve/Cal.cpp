#include"Cal.h"
#include<iostream>

cal::cal(vector<levelbond> input){ levelterm = input; }
void cal::spot_rate(){ 
	vector<levelbond>::iterator iter = levelterm.begin();
	(*iter).vtmcalculator();
	spot_termstructure.push_back(((*iter).get_ytm()+0.00000));//get the first spot
	double p = 1 / (1 + (*iter).get_ytm() / 2);
	iter++;// point to following terms;
   // cout << p<<endl;
	for (; iter!=levelterm.end(); iter++)
	{
		(*iter).calculator(((*iter).get_coupon())*1000*p);// 调用本levelbond的实例的计算函数，输入参数为前面的累计金额previous_value
		spot_termstructure.push_back((*iter).get_spot());
		double den = 1 + (*iter).get_spot() / 2;
		p+=1/pow(den, (*iter).get_paymentnum());	
	}
}
void cal::discount_factor(){ 
	vector<double>::iterator t = spot_termstructure.begin();
	double times = 0;
	for (; t != spot_termstructure.end(); t++)
	{
		double temp2=pow(1.0 + (*t)/ 2.0, -(times + 1) / 2.0); 
		double temp = 1/pow((1 + (*t) ), times);
		discount_termstructure.push_back(temp2);
		times++;
	}
}
void cal::forward(){

	for (int i = 0; i < spot_termstructure.size() - 1; ++i)
	{
		double temp= (pow((pow(1.0 + spot_termstructure[i + 1] / 2.0, (i + 2) / 2.0) /
			pow(1.0 + spot_termstructure[i]/ 2.0, (i + 1) / 2.0)), 1.0 / 0.5) - 1.0) * 100.0 * 2.0;
		forward_termstructure.push_back(temp);
	}
	forward_termstructure.push_back(0.0000);
}

vector<double>cal::get_finalresult(){
	return spot_termstructure;
};
vector<double>cal::get_discount(){return discount_termstructure;}
vector<double>cal::get_forward(){ return forward_termstructure; }
