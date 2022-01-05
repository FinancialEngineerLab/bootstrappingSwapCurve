#include<iostream>
#include<fstream>
#include"levelbond.h"
#include"Cal.h"
#include<vector>
#include <iomanip>
using namespace std;
int main()
{   
	cout << "Project #2--Extracting Spot Rate From Yield Curve." << endl;
	//construct vector and put the given levelbond into the vector;
	ifstream infile;
	infile.open("data.txt");
	double x;
	int mark = 1;
	vector<levelbond> bondseries;
	vector<levelbond>::iterator bondseries_iter = bondseries.begin();
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
				cout << '\t' << '\t' << '\t' << "---***End of The File***---" << endl;
			}
			else
				cout << "Wrong file!" << endl;
		}
	}
	for (int i = 0, j=0; i < parameters.size()/3; i++)
	{
		levelbond temp(parameters[j], parameters[j+1], parameters[j+2]);
		bondseries.push_back(temp);
		j += 3;
	}
	// To construct a complete term of levelbond, which is more easy to compute!
	for (bondseries_iter = bondseries.begin(); bondseries_iter != bondseries.end()-1; bondseries_iter++)
	{
		double m = ((*(bondseries_iter + 1)).get_maturity() - (*(bondseries_iter)).get_maturity()) / 0.5 - 1;
		if (m > 0)
		{
			double insert_coupon = (*(bondseries_iter)).get_rawcoupon() + ((*(bondseries_iter + 1)).get_rawcoupon() - (*(bondseries_iter)).get_rawcoupon()) / (m + 1); //calculate the coupon of the inserted bond;
			double insert_maturity = (*bondseries_iter).get_maturity() + 0.5;// every inserted bond's maturity will be 0.5year+last one's maturity e.g 1-->1.5 and so on;
			levelbond insertbond(insert_coupon, 100, insert_maturity);//create the inserted bond
			bondseries.insert((bondseries_iter + 1), insertbond); // Inserted!!!!
			bondseries_iter = bondseries.begin();//NOTE: Why I do this? since when I add an element into the vector, the previous iterator becomes useless, it will point to strange things, so basically, I am assigning a new iterator for this  "relative new "vector!
		}
		//now we have a new vector consists levelbond with iterval of 6 months!!!!!!
	}

	cal calculator(bondseries);
	calculator.spot_rate();
	calculator.discount_factor();
	calculator.forward();

	vector<double> spotrate = calculator.get_finalresult();
	vector<double> discount = calculator.get_discount();
	vector<double> forward = calculator.get_forward();
	vector<double>::iterator it_1 = spotrate.begin();
	vector<double>::iterator it_2 = discount.begin();
	vector<double>::iterator it_3 = forward.begin();
	cout << endl;
	cout<< "Tenor(M)" << "\t" << "Spot_Rate(%)" << "\t"  <<"\t"<< "Discount_Factor(%)"<<"\t"<< "\t" <<"Forward(%)"<< endl;
	int m = 1; 
	for (; it_1 != spotrate.end(); it_1++, it_2++, it_3++,++m)
	{
		cout << setprecision(4) <<m*6<<"\t"<<"\t"<<"\t"<< (*it_1) * 100<<"\t" << "\t" << "\t" << (*it_2) <<"\t"<< "\t" << "\t" << (*it_3) << endl;
	}






	return 1;
} 