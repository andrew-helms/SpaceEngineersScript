using System;
using System.Collections.Generic;
using System.Text;

namespace PID_Controller
{
    internal class PIDController
    {
        public PIDController(double Kp = 0, double Ki = 0, double Kd = 0)
        {
            m_Kp = Kp;
            m_Ki = Ki;
            m_Kd = Kd; 
            //m_FeedFoward = feedforwardFunc;

            m_AccumulatedError = 0;
            m_PrevSlope = 0;
        }

        public double Update(double error, double state = 0, double deltaTime = 1)
        {
            double output = error * m_Kp;

            output += m_AccumulatedError * m_Ki;

            double slope = (error - m_PrevError) / deltaTime;

            output += (slope + m_PrevSlope) / 2 * m_Kd;

            m_PrevSlope = slope;

            //if (m_FeedFoward != null)
            //{
            //    output += m_FeedFoward(state);
            //}

            m_PrevError = error;
            m_AccumulatedError *= 0.9; //slowly decay
            m_AccumulatedError += error * deltaTime;

            return output;
        }

        private double m_Kp;
        private double m_Ki;
        private double m_Kd;

        //private Func<double, double> m_FeedFoward;

        private double m_AccumulatedError;
        private double m_PrevError;
        private double m_PrevSlope;
    }
}
