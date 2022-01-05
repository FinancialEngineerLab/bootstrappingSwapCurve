#ifndef CAL_H
#define CAL_H
#include<vector>
using namespace std;
#include"levelbond.h"
class cal
{
public:
	cal();
	cal(vector<levelbond> inputbondseries);
	void spot_rate();
	void discount_factor();
	void forward();
	vector<double>get_finalresult();
	vector<double>get_discount();
	vector<double>get_forward();
private:
	vector<double> spot_termstructure;
	vector<double> discount_termstructure;
	vector<double> forward_termstructure;
	vector<levelbond> levelterm; 
   
	
};
#endif
